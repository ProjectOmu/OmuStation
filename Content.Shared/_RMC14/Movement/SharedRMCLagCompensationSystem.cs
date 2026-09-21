// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Movement/SharedRMCLagCompensationSystem.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using System.Numerics;
using Content.Omu.Common.CCVar;
using Content.Shared.Coordinates;
using Content.Shared.GameTicking;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Movement;

/// <summary>
///     Shared half of RMC-14's lag compensation: stores the last server tick (and physics substep)
///     each client had authoritative state for, and exposes the rewound-coordinate lookups that
///     adjudicators use.
/// </summary>
/// <remarks>
///     The base implementation deliberately performs *no* rewind — it returns live coordinates.
///     Only the server subclass rewinds, via <c>Content.Server.Movement.Systems.LagCompensationSystem</c>.
/// </remarks>
public abstract class SharedRMCLagCompensationSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    ///     Extra slack, in tiles, allowed when testing a lag-compensated hit.
    /// </summary>
    public float MarginTiles { get; private set; }

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    /// <summary>
    ///     How much of a session's measured round-trip time counts towards the rewind it may claim.
    ///     Matches the <c>Ping * 1.5</c> the pre-Omu rewind used.
    /// </summary>
    private const double PingRewindFactor = 1.5;

    private int _minimumPlausibleRewindMs;
    private int _substeps = 1;
    private float _substepTime;

    /// <summary>
    ///     Mirror of <c>omu.lag_compensation_milliseconds</c>. The server half pushes the same value
    ///     into <c>LagCompensationSystem.BufferTime</c>; this copy exists so that the clamp in
    ///     <see cref="SetLastRealTick"/> can live in shared code.
    /// </summary>
    private int _lagCompensationMilliseconds;

    /// <summary>
    ///     Debug switch for the predicted-hit adjudicator. Left off; flip it by hand when tuning.
    /// </summary>
    private readonly bool _logPrediction = false;

    private readonly Dictionary<NetUserId, (GameTick Tick, int Substep)> _lastRealTicks = new();

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();

        SubscribeNetworkEvent<RMCSetLastRealTickEvent>(OnSetLastRealTick);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        _player.PlayerStatusChanged += OnPlayerStatusChanged;

        Subs.CVar(_config, OmuCVars.LagCompensationMarginTiles, v => MarginTiles = v, true);
        Subs.CVar(_config, OmuCVars.LagCompensationMinRewindMilliseconds, v => _minimumPlausibleRewindMs = v, true);
        Subs.CVar(_config, OmuCVars.LagCompensationMilliseconds, v => _lagCompensationMilliseconds = v, true);
        Subs.CVar(_config, CVars.NetTickrate, UpdateSubsteps, true);
        Subs.CVar(_config, CVars.TargetMinimumTickrate, UpdateSubsteps, true);
    }

    private void OnSetLastRealTick(RMCSetLastRealTickEvent msg, EntitySessionEventArgs args)
    {
        // RMC-14 steps back one tick from the reported value: the client renders interpolated
        // state, so the frame it actually reacted to is at least one tick behind the newest state
        // it had received. SetLastRealTick clamps the result — the client is not trusted here.
        SetLastRealTick(args.SenderSession.UserId, msg.Tick - 1, msg.Substep);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    /// <summary>
    ///     Forgets a session's claim when it disconnects.
    /// </summary>
    /// <remarks>
    ///     Entries are keyed by <see cref="NetUserId"/>, which outlives the
    ///     connection, so without this a reconnecting player resumes the claim they left behind - and
    ///     under <see cref="GetRewindOffset"/> an arbitrarily old claim is worth the session's full
    ///     allowance rather than nothing. The adjacent <c>_predicted</c> store in the server's
    ///     GunPredictionSystem is already cleaned up this way.
    /// </remarks>
    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _lastRealTicks.Remove(args.Session.UserId);
    }

    /// <summary>
    ///     Forgets every stored claim when the round restarts.
    /// </summary>
    /// <remarks>
    ///     The other half of the same problem. Disconnect removal above handles the common case;
    ///     this bounds the store for anyone still connected across a restart, and makes every session
    ///     start a round with no claim until its next heartbeat, which in combat is at most three
    ///     ticks away.
    /// </remarks>
    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _lastRealTicks.Clear();
    }

    private void UpdateSubsteps(int _)
    {
        var targetMinTickrate = (float) _config.GetCVar(CVars.TargetMinimumTickrate);
        var serverTickrate = (float) _config.GetCVar(CVars.NetTickrate);
        _substeps = Math.Max(1, (int) Math.Ceiling(targetMinTickrate / serverTickrate));
        _substepTime = 1.0f / serverTickrate / _substeps;
    }

    private static float AABBDistanceSquared(Box2 a, Box2 b)
    {
        var xDist = Math.Max(a.Left - b.Right, b.Left - a.Right);
        var yDist = Math.Max(a.Bottom - b.Top, b.Bottom - a.Top);

        xDist = Math.Max(0, xDist);
        yDist = Math.Max(0, yDist);

        return xDist * xDist + yDist * yDist;
    }

    public virtual (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid,
        ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        return (xform.Coordinates, xform.LocalRotation);
    }

    public virtual Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, session, xform);
        return angle;
    }

    public virtual EntityCoordinates GetCoordinates(EntityUid uid,
        ICommonSession? session,
        TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, session, xform);
        return coordinates;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid,
        EntityUid? session,
        TransformComponent? xform = null)
    {
        if (!_actorQuery.TryComp(session, out var actor))
            return GetCoordinates(uid, (ICommonSession?) null, xform);

        return GetCoordinates(uid, actor.PlayerSession, xform);
    }

    /// <summary>
    ///     True if <paramref name="lagCompensatedTarget"/> is within <paramref name="range"/> of
    ///     <paramref name="sessionEnt"/>, allowing <see cref="MarginTiles"/> of extra slack if (and
    ///     only if) the target actually moved since the perspective session saw it.
    /// </summary>
    public bool IsWithinMargin(Entity<TransformComponent?> sessionEnt,
        Entity<TransformComponent?> lagCompensatedTarget,
        ICommonSession? session,
        float range)
    {
        var targetCoords = GetCoordinates(lagCompensatedTarget, session);
        if (_net.IsServer)
        {
            var targetCurrentCoords = lagCompensatedTarget.Owner.ToCoordinates();
            if (!_transform.InRange(targetCoords, targetCurrentCoords, 0.01f))
                range += MarginTiles;
        }

        return _transform.InRange(sessionEnt.Owner.ToCoordinates(), targetCoords, range);
    }

    public virtual GameTick GetLastRealTick(NetUserId? session)
    {
        if (session == null || !_lastRealTicks.TryGetValue(session.Value, out var last))
            return _timing.CurTick;

        return last.Tick;
    }

    /// <summary>
    ///     How far back in real time <paramref name="session"/> is entitled to rewind <i>right now</i>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Omu addition. <see cref="SetLastRealTick"/> bounds
    ///     the claim at the instant it is stored, which is not the instant it is spent. The stored
    ///     tick is frozen while <c>CurTick</c> keeps advancing, so a client that simply stops
    ///     reporting grows its own rewind by one tick per tick, for free and with no ping to pay -
    ///     and the client decides when to report: the heartbeat is combat-mode gated,
    ///     rate-limited and value-change gated, so going quiet is a normal, unremarkable thing for
    ///     it to do. Before this method the only ceiling left on that growth was the position
    ///     buffer, which is the same number for everybody and so bounds nothing about this
    ///     particular client. Deriving the offset here means it is re-checked against the session's
    ///     own connection every time it is used.
    ///     </para>
    ///     <para>
    ///     Over-deep claims are clamped to what the session could justify rather than dropped to
    ///     zero. A client that goes quiet then fires should get the rewind its latency actually
    ///     earns, not a sudden and invisible loss of all compensation.
    ///     </para>
    ///     <para>
    ///     The substep is floored at zero. It exists to move the rewind <i>forward</i>, towards now,
    ///     by the fraction of a tick the client was mid-way through; a negative value would deepen
    ///     the rewind by up to a full tick, and it is applied after every bound above. Nothing
    ///     produces a negative substep legitimately - <see cref="GetCurrentSubstep"/> derives it from
    ///     elapsed physics time - but it arrives off the wire, so it is bounded here rather than
    ///     trusted.
    ///     </para>
    /// </remarks>
    public TimeSpan GetRewindOffset(NetUserId? session)
    {
        if (session == null || !_lastRealTicks.TryGetValue(session.Value, out var last))
            return TimeSpan.Zero;

        var curTick = _timing.CurTick;
        if (last.Tick.Value >= curTick.Value)
            return TimeSpan.Zero;

        var offset = curTick.Value - last.Tick.Value;

        var maxOffset = Math.Min(MaxRewindTicks(), MaxPlausibleOffsetTicks(session.Value));
        if (offset > maxOffset)
            offset = maxOffset;

        var offsetTime = offset * _timing.TickPeriod;

        // MaxRewindTicks rounds up, so the tick bound alone can name a hair more time than the
        // buffer actually holds. Cap the duration too, or a legitimate maximum-depth rewind trips
        // the consumer's "older than the buffer" guard and silently degrades to no rewind at all.
        var bufferTime = TimeSpan.FromMilliseconds(_lagCompensationMilliseconds);
        if (offsetTime > bufferTime)
            offsetTime = bufferTime;

        var substep = Math.Max(0, last.Substep);
        if (substep != 0)
            offsetTime -= SubstepPeriod * substep;

        return offsetTime < TimeSpan.Zero ? TimeSpan.Zero : offsetTime;
    }

    /// <summary>
    ///     The physics substep the client was on when it reported its last real tick.
    /// </summary>
    /// <remarks>
    ///     This is the raw stored value, bounded in both directions by one tick's worth of substeps.
    ///     It is what the projectile-side substep interpolation wants - that one legitimately moves
    ///     the <i>projectile</i> either way along its own path. The rewind-depth consumer must use
    ///     <see cref="GetRewindOffset"/>, which floors it; see the note there.
    /// </remarks>
    public int GetLastRealSubstep(NetUserId? session)
    {
        if (session == null || !_lastRealTicks.TryGetValue(session.Value, out var last))
            return 0;

        return last.Substep;
    }

    /// <summary>
    ///     Records a client's claim about how far behind the server it is.
    /// </summary>
    /// <remarks>
    ///     Omu addition. Upstream RMC-14/CMU write the client's claim
    ///     verbatim, leaving a client free to name any <see cref="GameTick"/> at all; the only thing
    ///     bounding it was the "rewind longer than the buffer degrades to no rewind" ceiling in
    ///     <c>LagCompensationSystem</c>. Clamping here — at the single write point — means every
    ///     present and future consumer of <see cref="GetLastRealTick"/> inherits the bound, rather
    ///     than each adjudicator having to remember to re-check it.
    ///     </para>
    ///     <para>
    ///     This clamp bounds the claim <i>as of the moment it is made</i>, and that is all it can do:
    ///     the tick stored here is spent later, against a <c>CurTick</c> that has moved on. The bound
    ///     that matters is re-applied at consumption by <see cref="GetRewindOffset"/>. Keeping this
    ///     one as well means a nonsense claim never enters the store in the first place.
    ///     </para>
    /// </remarks>
    public void SetLastRealTick(NetUserId session, GameTick tick, int substep = 0)
    {
        if (_net.IsClient)
            return;

        var curTick = _timing.CurTick;

        // A client can never legitimately be ahead of the server.
        if (tick.Value > curTick.Value)
            tick = curTick;

        // ...nor further behind than the position history we actually keep...
        var maxOffset = MaxRewindTicks();

        // ...nor further behind than this session's own connection could account for.
        maxOffset = Math.Min(maxOffset, MaxPlausibleOffsetTicks(session));

        var earliest = curTick.Value > maxOffset ? new GameTick(curTick.Value - maxOffset) : GameTick.Zero;
        if (tick.Value < earliest.Value)
            tick = earliest;

        _lastRealTicks[session] = (tick, Math.Clamp(substep, -_substeps, _substeps));
    }

    /// <summary>
    ///     The largest rewind, in ticks, that <paramref name="session"/>'s measured connection could
    ///     plausibly justify.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Omu addition. The buffer ceiling above bounds how far back the
    ///     position history reaches, but it is the same number for everybody, so on its own it lets
    ///     any client claim the deepest rewind the server can serve. That is a change in kind from
    ///     the code this replaced: the rewind used to be derived from <c>Channel.Ping * 1.5</c>, a
    ///     figure the <i>server</i> measures, so a client on a fast connection simply could not ask
    ///     for a deep rewind. Without this bound a 20 ms client can hit targets where they stood
    ///     <see cref="OmuCVars.LagCompensationMilliseconds"/> ago, on melee and hitscan, with the
    ///     gun-prediction feature switched off.
    ///     </para>
    ///     <para>
    ///     The factor is the same 1.5 the replaced formula used - one round trip plus the half-trip
    ///     of slack it allowed for. The floor beneath it is
    ///     <see cref="OmuCVars.LagCompensationMinRewindMilliseconds"/>, which exists because a claim
    ///     can be legitimately stale even at zero ping: the client reports its tick on a throttled
    ///     heartbeat (one send per three ticks, ~100 ms at 30 tps), so a fresh claim is routinely
    ///     that old before jitter. A floor below that would punish honest low-ping clients for the
    ///     throttle.
    ///     </para>
    ///     <para>
    ///     This deliberately bounds the <i>claim</i>, not the honesty of it. A client can still
    ///     understate its tick, which only ever costs it accuracy.
    ///     </para>
    ///     <para>
    ///     <b>Known residual.</b> <c>Channel.Ping</c> is timed by the server but paced by the client:
    ///     the measured round trip includes the client's own decision of when to answer a ping, so a
    ///     modified client can inflate it without real latency and widen this allowance. That is
    ///     parity with the pre-Omu <c>Ping * 1.5</c> rewind, not a regression, and unlike a silent
    ///     claim an inflated ping is visible in the admin player list. Removing the dependency
    ///     entirely would mean deriving the allowance from server-observed packet arrival instead,
    ///     which is a larger change than this bound.
    ///     </para>
    /// </remarks>
    private uint MaxPlausibleOffsetTicks(NetUserId session)
    {
        var tickPeriod = _timing.TickPeriod.TotalMilliseconds;
        if (tickPeriod <= 0)
            return MaxRewindTicks();

        var allowanceMs = (double) _minimumPlausibleRewindMs;
        if (_player.TryGetSessionById(session, out var found))
            allowanceMs = Math.Max(allowanceMs, found.Channel.Ping * PingRewindFactor);

        // Ceiling, to match MaxRewindTicks. Truncating here would make the plausibility bound one
        // tick tighter than the buffer even when the two are configured to the same duration, which
        // reads as an off-by-one bug rather than as policy.
        return (uint) Math.Max(0, Math.Ceiling(allowanceMs / tickPeriod));
    }

    /// <summary>
    ///     How many ticks of rewind the position buffer can actually satisfy.
    /// </summary>
    public uint MaxRewindTicks()
    {
        var tickPeriod = _timing.TickPeriod.TotalMilliseconds;
        if (tickPeriod <= 0)
            return 0;

        return (uint) Math.Max(0, Math.Ceiling(_lagCompensationMilliseconds / tickPeriod));
    }

    public void SendLastRealTick()
    {
        if (_net.IsServer || !_timing.IsFirstTimePredicted)
            return;

        RaiseNetworkEvent(new RMCSetLastRealTickEvent(GetLastRealTick(null), GetClientSubstep()));
    }

    /// <summary>
    ///     Unions the AABBs of every fixture on <paramref name="entity"/> whose collision layer is
    ///     caught by <paramref name="mask"/>, evaluated at <paramref name="position"/>.
    /// </summary>
    /// <param name="mask">Collision mask to filter by, or null to accept every fixture.</param>
    /// <returns>False if the entity has no fixture matching the mask.</returns>
    public bool TryGetBoundsAt(Entity<FixturesComponent?> entity,
        Vector2 position,
        int? mask,
        out Box2 bounds)
    {
        bounds = new Box2(position, position);

        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        var any = false;
        var transform = new Transform(position, 0f);
        foreach (var fixture in entity.Comp.Fixtures.Values)
        {
            if (mask != null && (fixture.CollisionLayer & mask.Value) == 0)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                bounds = bounds.Union(fixture.Shape.ComputeAABB(transform, i));
                any = true;
            }
        }

        return any;
    }

    /// <summary>
    ///     Does <paramref name="projectile"/> overlap <paramref name="target"/>, with the target
    ///     rewound to where <paramref name="perspectiveSession"/> last saw it?
    /// </summary>
    /// <remarks>
    ///     Called by the server's <c>GunPredictionSystem</c> - once to adjudicate a claimed hit, and
    ///     once for the ongoing re-check of a projectile already in flight. The client's claimed
    ///     position is deliberately not a parameter: the target is rewound from the session's
    ///     reported tick and everything else is derived server-side.
    /// </remarks>
    public bool Collides(Entity<FixturesComponent?> target,
        Entity<PhysicsComponent?> projectile,
        ICommonSession? perspectiveSession,
        int substep = 0)
    {
        if (!Resolve(target, ref target.Comp, false) ||
            !Resolve(projectile, ref projectile.Comp, false))
        {
            return false;
        }

        substep = Math.Clamp(substep, -_substeps, _substeps);

        var projectileCoordinates = _transform.GetMapCoordinates(projectile.Owner);
        var projectileVelocity = _physics.GetLinearVelocity(projectile.Owner, projectile.Comp.LocalCenter, projectile.Comp);
        var substeppedProjectilePos = projectileCoordinates.Position +
                                      projectileVelocity / _timing.TickRate * (substep / (float) _substeps);

        var targetCoordinates = _transform.ToMapCoordinates(GetCoordinates(target.Owner, perspectiveSession));
        TryGetBoundsAt(target, targetCoordinates.Position, projectile.Comp.CollisionMask, out var targetBounds);

        var projectileBounds = new Box2(substeppedProjectilePos, substeppedProjectilePos);
        if (_fixturesQuery.TryComp(projectile.Owner, out var projFixtureComp))
            TryGetBoundsAt((projectile.Owner, projFixtureComp), substeppedProjectilePos, null, out projectileBounds);

        if (_logPrediction)
        {
            Log.Debug($"""
                Lag comp collide data:
                  Session Name:   {perspectiveSession}
                  Pre-Substep
                    Proj Coords:  {projectileCoordinates}
                  CurTick:        {_timing.CurTick}
                  Substep:        {substep}
                  Projectile Pos: {substeppedProjectilePos}
                  Target Pos:     {targetCoordinates.Position}
                  Proj AABB:      {projectileBounds.BottomLeft}
                                  {projectileBounds.TopRight}
                  Target AABB:    {targetBounds.BottomLeft}
                                  {targetBounds.TopRight}
                  AABB Intersect? {targetBounds.Intersects(projectileBounds)}
                  AABB Distance:  {Math.Sqrt(AABBDistanceSquared(targetBounds, projectileBounds))}
                """);
        }

        if (targetBounds.Intersects(projectileBounds))
            return true;

        if (AABBDistanceSquared(targetBounds, projectileBounds) <= MarginTiles * MarginTiles)
            return true;

        if (_logPrediction)
            Log.Warning($"Predicted hit denied for session '{perspectiveSession}'");

        return false;
    }

    /// <summary>
    ///     Single validation point for predicted-hit messages, so every future claimant rewinds the
    ///     session identically before testing collision.
    /// </summary>
    /// <remarks>
    ///     Currently unused: the server adjudicates claims through <see cref="Collides"/> directly,
    ///     with the surrounding fixture, collision-rule and line-of-sight checks that this wrapper
    ///     does not perform. Kept as the single place to put those if a second claimant ever needs
    ///     them. Note it takes a caller-supplied tick and routes it through
    ///     <see cref="SetLastRealTick"/>, so a future caller inherits the bounds rather than
    ///     bypassing them.
    /// </remarks>
    public bool ValidatePredictedHit(Entity<FixturesComponent?> target,
        Entity<PhysicsComponent?> projectile,
        ICommonSession? session,
        GameTick lastRealTick,
        int substep)
    {
        if (session != null)
            SetLastRealTick(session.UserId, lastRealTick, substep);

        return Collides(target, projectile, session, substep);
    }

    public int? GetCurrentSubstep()
    {
        if (_physics.EffectiveCurTime is not { } physicsTime)
            return null;

        var diff = physicsTime - _timing.CurTime;
        return (int) Math.Round(diff.TotalSeconds / _substepTime);
    }

    public int GetSubsteps()
    {
        return _substeps;
    }

    /// <summary>
    ///     Duration of a single physics substep, for fractional tick rewinds.
    /// </summary>
    public TimeSpan SubstepPeriod => TimeSpan.FromSeconds(_substepTime);

    public int GetClientSubstep()
    {
        return GetCurrentSubstep() ?? 0;
    }
}

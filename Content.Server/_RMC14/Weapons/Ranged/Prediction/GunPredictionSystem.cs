// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Server/_RMC14/Weapons/Ranged/Prediction/GunPredictionSystem.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Content.Omu.Common.CCVar;
using Content.Server._RMC14.Movement;
using Content.Server.Movement.Components;
using Content.Server.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.GameTicking;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics; // Omu - Fixture, for re-deriving contact filtering
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Server half of gun prediction: pairs each authoritative projectile with the client-side copy
///     the shooter already drew for it, and adjudicates the hits that client reports by rewinding
///     the target to where it was when the shot was sent.
/// </summary>
/// <remarks>
///     <para>
///     <b>This is the client-trust surface of the whole feature.</b> Two things arrive here from a
///     client and are believed only after checking: the list of predicted ids attached to a shot
///     (<see cref="GunProjectilesShotEvent"/>) and the hit reports
///     (<see cref="PredictedProjectileHitEvent"/>). Neither is validated by the engine. Every
///     untrusted-input rule below is spelled out at its check; the invariant they add up to is that
///     a client can only ever affect projectiles the server itself spawned for <i>that client's own
///     session</i>, and only by asking for a collision the server independently re-derives.
///     </para>
///     <para>
///     <b>Deviations from the source</b> (RMC-14 / ColonialMarinesUniverse):
///     </para>
///     <list type="bullet">
///         <item>
///         <b>No <c>RequestShootEvent</c> subscription.</b> The source subscribed to it here to do
///         two jobs. Omu's <c>SharedGunSystem</c> already owns that event and applies four
///         fork-specific rules inside it (multishot, mech pilots, held-item blocking, Goobstation
///         burst target locking); a second subscriber would fire the gun twice, because the bus does
///         not stop dispatching after the first handler. The two jobs are re-homed instead:
///         applying the client's <c>LastRealTick</c> is done by
///         <c>Content.Server.Weapons.Ranged.Systems.GunSystem</c>'s override of
///         <c>OnShootRequestReceived</c>, and pairing projectiles with client ids is done here from
///         <see cref="GunProjectilesShotEvent"/>, which <c>SharedGunSystem</c> raises after the shot.
///         </item>
///         <item>
///         <b>No <c>RMCLagCompensationSystem</c> dependency.</b> The source used it only inside the
///         deleted <c>RequestShootEvent</c> handler, to record the shooter's last real tick. That
///         call now lives in <c>GunSystem</c>, so the dependency falls out entirely.
///         </item>
///         <item>
///         <b><see cref="OmuCVars"/> instead of <c>RMCCVars</c></b>, and
///         <b><see cref="ProjectileSystem"/> (the concrete server system) instead of
///         <c>SharedProjectileSystem</c></b>, because Omu's <c>ProjectileCollide</c> is declared on
///         the server system rather than the shared one.
///         </item>
///         <item>
///         <b>Everything observable is gated on <see cref="SharedGunPredictionSystem.GunPrediction"/></b>,
///         because <c>omu.gun_prediction</c> defaults to <c>false</c> here (the source ships it on).
///         Each gate is documented where it appears.
///         </item>
///     </list>
/// </remarks>
public sealed partial class GunPredictionSystem : SharedGunPredictionSystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ProjectileSystem _projectile = default!;
    [Dependency] private readonly RMCLagCompensationSystem _lagCompensation = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    /// <summary>
    ///     Server projectiles indexed by the session that fired them and the client-side id that
    ///     session reported for them.
    /// </summary>
    /// <remarks>
    ///     <b>The <see cref="NetUserId"/> half of the key is the containment.</b> Client-side ids are
    ///     picked by the client and are therefore arbitrary; scoping them by session means a client
    ///     that guesses or replays another player's ids still only ever resolves to its own
    ///     projectiles, or to nothing.
    ///     <para>
    ///     Deviation: the source keyed on a raw <see cref="System.Guid"/> obtained through
    ///     <see cref="NetUserId"/>'s implicit conversion. Keying on <see cref="NetUserId"/> itself is
    ///     the same value with the intent visible, and removes a silent conversion on a security
    ///     boundary.
    ///     </para>
    /// </remarks>
    private readonly Dictionary<(NetUserId Shooter, int ClientId), EntityUid> _predicted = new();

    /// <summary>
    ///     Hit reports received this tick, drained in <see cref="Update"/>.
    /// </summary>
    /// <remarks>
    ///     Buffered rather than handled inline so adjudication happens at a known point in the tick,
    ///     after physics has moved everything. Cleared unconditionally every tick, so a client that
    ///     spams reports cannot grow it without bound.
    /// </remarks>
    private readonly List<(PredictedProjectileHitEvent Event, ICommonSession Player)> _predictedHits = new();

    private bool _preventCollision;
    private bool _logHits;

    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<LagCompensationComponent> _lagCompensationQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<PredictedProjectileServerComponent> _predictedProjectileServerQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<IgnorePredictionHitComponent> _ignorePredictionHitQuery;

    /// <summary>
    ///     Most targets a single hit report may name.
    /// </summary>
    /// <remarks>
    ///     A real projectile overlaps very few things at once; a report naming hundreds is either a
    ///     bug or an attempt to make one packet cost the server an unbounded number of collision
    ///     tests. Entries past this are dropped. Generous enough that a legitimate shotgun pellet in
    ///     a crowd is never truncated.
    /// </remarks>
    private const int MaxClaimedHitsPerReport = 16;

    /// <summary>
    ///     What counts as "solid" when testing whether a claimed hit was reachable.
    /// </summary>
    private const CollisionGroup ObstructionMask = CollisionGroup.Impassable | CollisionGroup.BulletImpassable;


    public override void Initialize()
    {
        base.Initialize();

        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _lagCompensationQuery = GetEntityQuery<LagCompensationComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _predictedProjectileServerQuery = GetEntityQuery<PredictedProjectileServerComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        _ignorePredictionHitQuery = GetEntityQuery<IgnorePredictionHitComponent>();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        // Deviation: the source subscribed to RequestShootEvent here. See the class remarks - Omu's
        // SharedGunSystem already owns that event, so we take the shot's output instead of the shot's
        // request, and the gun fires exactly once.
        SubscribeLocalEvent<GunProjectilesShotEvent>(OnGunProjectilesShot);

        SubscribeNetworkEvent<PredictedProjectileHitEvent>(OnPredictedProjectileHit);

        SubscribeLocalEvent<PredictedProjectileServerComponent, MapInitEvent>(OnPredictedMapInit);
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentRemove>(OnPredictedRemove);
        SubscribeLocalEvent<PredictedProjectileServerComponent, EntityTerminatingEvent>(OnPredictedRemove);
        SubscribeLocalEvent<PredictedProjectileServerComponent, PreventCollideEvent>(OnPredictedPreventCollide);

        // Deviation: OmuCVars, not RMCCVars. Same five knobs, renamed without the RMC prefix.
        Subs.CVar(_config, OmuCVars.GunPredictionPreventCollision, v => _preventCollision = v, true);
        Subs.CVar(_config, OmuCVars.GunPredictionLogHits, v => _logHits = v, true);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _predicted.Clear();
    }

    /// <summary>
    ///     Attaches a <see cref="PredictedProjectileServerComponent"/> to each projectile a shot just
    ///     spawned, pairing it with the client-side copy the shooter drew for it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <b>This replaces the source's <c>MarkPredicted</c> local function inside
    ///     <c>GunSystem.Shoot</c>.</b> Same rule, moved out of the gun code and behind
    ///     <see cref="GunProjectilesShotEvent"/> so Omu's heavily-forked shoot path does not have to
    ///     carry prediction state. The pairing is identical: positional, by index.
    ///     </para>
    ///     <para>
    ///     <b>Index-matching rule:</b> <c>Spawned[i]</c> pairs with <c>PredictedIds[i]</c>. Both lists
    ///     are in the order the respective side fired them, and both sides walk the same shot in the
    ///     same order, so index is the only correspondence available - there is no id on the server
    ///     projectile to match against, which is the entire reason this pairing exists.
    ///     </para>
    ///     <para>
    ///     <b><see cref="GunProjectilesShotEvent.PredictedIds"/> is untrusted.</b> It arrived in a
    ///     <c>RequestShootEvent</c> from the client and has not been validated by anything. Every
    ///     malformed shape is handled by construction rather than by rejection, because a
    ///     disagreement about shot count is normal (the server may fire fewer shots than the client
    ///     guessed, e.g. the magazine ran dry mid-burst) and must not drop the shot:
    ///     </para>
    ///     <list type="bullet">
    ///         <item><b>null or empty</b> - the shooter is not predicting. Nothing is paired; every
    ///         projectile stays a plain server projectile. This is also the state the whole feature is
    ///         in when <c>omu.gun_prediction</c> is off.</item>
    ///         <item><b>shorter than <c>Spawned</c></b> - the first <c>PredictedIds.Count</c>
    ///         projectiles are paired and the surplus server projectiles are left unpaired, i.e.
    ///         fully server-authoritative. A client gains nothing by sending a short list, because an
    ///         unpaired projectile is exactly the pre-port behaviour.</item>
    ///         <item><b>longer than <c>Spawned</c></b> - the surplus ids are ignored. The loop is
    ///         bounded by <c>Spawned.Count</c>, so a client cannot make the server allocate or track
    ///         more projectiles than it actually fired.</item>
    ///         <item><b>duplicate or replayed ids</b> - accepted, and resolved last-writer-wins in
    ///         <see cref="_predicted"/> (see <see cref="OnPredictedMapInit"/>). The earlier projectile
    ///         keeps its component but becomes unreachable by id, so it can no longer be claimed.
    ///         That fails closed: the worst a client achieves by reusing an id is losing the ability
    ///         to report a hit for one of its own projectiles.</item>
    ///         <item><b>negative or absurd ids</b> - harmless. The id is only ever used as a
    ///         dictionary key here and as a local <c>EntityUid</c> on the client; the server never
    ///         resolves it to an entity.</item>
    ///     </list>
    /// </remarks>
    private void OnGunProjectilesShot(ref GunProjectilesShotEvent ev)
    {
        // Gate 1 (server): with prediction off, no projectile is ever marked, so nothing downstream
        // on either side can engage - the client never sees a PredictedProjectileServerComponent, so
        // it hides no sprites, and no hit report can ever resolve to a projectile.
        if (!GunPrediction)
            return;

        if (ev.PredictedIds is not { Count: > 0 } predictedIds)
            return;

        // Gate 2: a gun can opt out of pairing. This is the only place tree-wide that honours
        // GunIgnorePredictionComponent - without it the component would be inert entirely. Refusing
        // to pair here is sufficient *for the server*: an unpaired projectile is a plain server
        // projectile, so the client never hides its sprite and no claimed hit can ever resolve to it.
        //
        // TODO Omu gun prediction: it is NOT sufficient for the shooter's screen. The client's
        // PredictShot/ShootPredicted do not check this component, so the copies are still spawned;
        // with nothing paired, the client's OnServerProjectileRemove never fires for them and they
        // are never retired, leaving a ghost bullet next to the real one until the prototype's
        // TimedDespawn expires. See the remarks on GunIgnorePredictionComponent.
        if (HasComp<GunIgnorePredictionComponent>(ev.Gun))
            return;

        // The component records the shooter's body so the client can tell "my bullet" from "someone
        // else's". A session with no attached entity has nothing to compare against, and is also not
        // a state a real shot can be fired from.
        if (ev.Shooter.AttachedEntity is not { } clientEnt)
            return;

        var pairs = Math.Min(ev.Spawned.Count, predictedIds.Count);
        for (var i = 0; i < pairs; i++)
        {
            var projectile = ev.Spawned[i];

            // A projectile can already be gone by the time we get here - something in the shoot path
            // may have deleted it (a shield eating it, an immediate detonation). Marking a dead
            // entity would register a dictionary entry that nothing ever cleans up.
            if (TerminatingOrDeleted(projectile))
                continue;

            var predicted = new PredictedProjectileServerComponent
            {
                Shooter = ev.Shooter,
                ClientId = predictedIds[i],
                ClientEnt = clientEnt,
            };

            AddComp(projectile, predicted, true);
            Dirty(projectile, predicted);
        }
    }

    /// <remarks>
    ///     Registration happens on <see cref="MapInitEvent"/> rather than inline in
    ///     <see cref="OnGunProjectilesShot"/> so that any other system which attaches the component is
    ///     tracked too. The engine raises <see cref="MapInitEvent"/> per-component when a component is
    ///     added to an already map-initialized entity, so this still fires for projectiles that are
    ///     marked after they were spawned, which is every projectile this system marks.
    /// </remarks>
    private void OnPredictedMapInit(Entity<PredictedProjectileServerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Shooter == null)
        {
            Log.Warning($"{nameof(PredictedProjectileServerComponent)} map initialized with a null shooter session!");
            return;
        }

        // Last-writer-wins on a duplicate client id: see the untrusted-input notes on
        // OnGunProjectilesShot. Overwriting orphans the older projectile, which fails closed.
        _predicted[(ent.Comp.Shooter.UserId, ent.Comp.ClientId)] = ent;
    }

    private void OnPredictedRemove<T>(Entity<PredictedProjectileServerComponent> ent, ref T args)
    {
        if (ent.Comp.Shooter == null)
            return;

        _predicted.Remove((ent.Comp.Shooter.UserId, ent.Comp.ClientId));
    }

    private void OnPredictedProjectileHit(PredictedProjectileHitEvent ev, EntitySessionEventArgs args)
    {
        // Gate 2 (server): with prediction off, hit reports are dropped on arrival rather than
        // buffered and discarded later. A client running a predicting build against a non-predicting
        // server therefore costs nothing but the parse.
        if (!GunPrediction)
            return;

        _predictedHits.Add((ev, args.SenderSession));
    }

    private void OnPredictedPreventCollide(Entity<PredictedProjectileServerComponent> ent, ref PreventCollideEvent args)
    {
        // Gate 3 (server): omu.gun_prediction_prevent_collision, off by default as in the source.
        // This is the invasive half of resolving a disagreement between the engine's own collision
        // and the rewound adjudication, because it suppresses the engine's.
        if (!_preventCollision)
            return;

        if (args.Cancelled)
            return;

        var other = args.OtherEntity;
        if (!_lagCompensationQuery.TryComp(other, out _) ||
            !_fixturesQuery.TryComp(other, out var otherFixtures))
        {
            return;
        }

        if (!_physicsQuery.TryComp(ent, out var entPhysics))
            return;

        if (!IsSameMap(ent.Owner, other))
            return;

        // Path B, from the shooter's own perspective: the engine wants these two to collide now, but
        // if they do not overlap when the target is rewound to where that shooter last saw it, the
        // engine's collision is the one that is wrong.
        if (!_lagCompensation.Collides(
                (other, otherFixtures),
                (ent.Owner, entPhysics),
                ent.Comp.Shooter,
                ent.Comp.Shooter is { } shooter ? _lagCompensation.GetLastRealSubstep(shooter.UserId) : 0))
        {
            args.Cancelled = true;
        }
    }
    // Omu - Path A's ping-based Collides was deleted here in favour of Path B, the validator in
    // SharedRMCLagCompensationSystem that Phase 1 shipped for exactly this purpose. See the remarks
    // on ProcessPredictedHit for the comparison and what was given up.

    /// <summary>
    ///     Finds a fixture on the target that could actually have stopped this projectile: hard, and
    ///     with a collision layer the projectile's mask includes.
    /// </summary>
    /// <remarks>
    ///     These are the two conditions the engine itself applies before a projectile hit is even
    ///     considered - <c>ProjectileSystem.OnStartCollide</c> requires <c>args.OtherFixture.Hard</c>,
    ///     and the broadphase requires the layer/mask overlap. <see cref="Collides"/> checks neither:
    ///     it skips non-matching fixtures and then measures against whatever is left, which for a
    ///     target with no matching fixture at all is nothing, leaving a degenerate point box that the
    ///     AABB enlargement inflates into a multi-tile square. So the absence of this check does not
    ///     merely fail to reject - it rewards naming a target the bullet could never have hit with a
    ///     LARGER acceptance area than one it could.
    /// </remarks>
    private bool TryGetBlockingFixture(PhysicsComponent projectile, FixturesComponent targetFixtures, out Fixture fixture)
    {
        foreach (var candidate in targetFixtures.Fixtures.Values)
        {
            if (!candidate.Hard || (candidate.CollisionLayer & projectile.CollisionMask) == 0)
                continue;

            fixture = candidate;
            return true;
        }

        fixture = default!;
        return false;
    }

    /// <summary>
    ///     Re-derives the collision filtering the physics engine would have applied to this pairing,
    ///     by raising <see cref="PreventCollideEvent"/> on both entities as contact filtering does,
    ///     and reporting whether anything cancelled it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <b>This is not an optimisation, it is a rule.</b> <c>ProjectileCollide</c> is entered after
    ///     the point where the engine consults these subscribers, so a predicted hit that called it
    ///     directly would silently ignore every one of them. That includes
    ///     <c>RequireProjectileTargetSystem</c>, which is what makes a prone target and a target
    ///     behind cover un-hittable; <c>ProjectileComponent.IgnoredEntities</c>; <c>IgnoreShooter</c>;
    ///     and embedding. Without this a client can hit someone lying behind a table by reporting a
    ///     collision the server's own physics had just refused.
    ///     </para>
    ///     <para>
    ///     Both directions are raised because subscribers exist on both sides and either may cancel.
    ///     </para>
    /// </remarks>
    private bool IsCollisionPrevented(
        EntityUid projectile,
        PhysicsComponent projectileBody,
        EntityUid target,
        PhysicsComponent targetBody,
        Fixture targetFixture)
    {
        if (!_fixturesQuery.TryComp(projectile, out var projectileFixtures) ||
            !projectileFixtures.Fixtures.TryGetValue(SharedProjectileSystem.ProjectileFixture, out var projectileFixture))
        {
            // No projectile fixture means physics could not have produced this contact either, so
            // there is nothing legitimate to re-derive. Fail closed.
            return true;
        }

        var ours = new PreventCollideEvent(projectile, target, projectileBody, targetBody, projectileFixture, targetFixture);
        RaiseLocalEvent(projectile, ref ours);
        if (ours.Cancelled)
            return true;

        var theirs = new PreventCollideEvent(target, projectile, targetBody, projectileBody, targetFixture, projectileFixture);
        RaiseLocalEvent(target, ref theirs);
        return theirs.Cancelled;
    }

    /// <summary>
    ///     Whether anything solid sits between the projectile and the target it is claimed to have hit.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <see cref="RMCLagCompensationSystem.Collides"/> is a pure overlap test between the
    ///     projectile and the rewound target. Overlap is not reachability: on its own it accepts a
    ///     target standing a couple of tiles to the <i>side</i> of the bullet, on the far side of a
    ///     wall or window, because the margin reaches through it.
    ///     </para>
    ///     <para>
    ///     Omu: the ray ends at the target's <i>rewound</i> position, the
    ///     same position the overlap test accepts on, not its live one. An earlier version raycast
    ///     to the live position, so the two halves of the adjudication disagreed by exactly the
    ///     rewind distance and each could overrule the other - a target that ran behind cover after
    ///     the shot had a legitimate hit refused, and a target that ran out from behind cover could
    ///     have an illegitimate one accepted. Both steps now answer about the same target position.
    ///     </para>
    ///     <para>
    ///     Both the projectile and the target are excluded from the ray by predicate, so neither end
    ///     can be its own obstruction regardless of how large its fixtures are. Anything the ray does
    ///     find is by definition between the two.
    ///     </para>
    /// </remarks>
    /// <param name="projectile">The projectile whose claim is being adjudicated.</param>
    /// <param name="target">The entity the claim names.</param>
    /// <param name="perspective">
    ///     The session whose view of the world the target is rewound to. Null tests against the
    ///     live position.
    /// </param>
    private bool HasClearShot(EntityUid projectile, EntityUid target, ICommonSession? perspective)
    {
        var from = _transform.GetMapCoordinates(projectile);

        // Rewound, not live - see the remarks above. Invalid coordinates land in Nullspace and are
        // refused by the map check below, which is the fail-closed direction.
        var to = _transform.ToMapCoordinates(_lagCompensation.GetCoordinates(target, perspective));

        if (from.MapId != to.MapId || from.MapId == MapId.Nullspace)
            return false;

        var delta = to.Position - from.Position;
        var distance = delta.Length();

        // Coincident; there is no segment for anything to be in the middle of.
        if (distance <= float.Epsilon)
            return true;

        // Both ends are excluded by predicate rather than by shortening the ray. An earlier version
        // stopped the ray a fixed distance short of the target instead, which is wrong for any
        // target whose own fixtures are larger than that distance: the ray ends inside the target,
        // the target blocks itself, and every legitimate claim is refused.
        var ray = new CollisionRay(from.Position, delta / distance, (int) ObstructionMask);
        foreach (var _ in _physics.IntersectRayWithPredicate(
                     from.MapId,
                     ray,
                     distance,
                     candidate => candidate == projectile || candidate == target,
                     returnOnFirstHit: true))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Adjudicates one hit report from one client.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <b>Every field of <paramref name="ev"/> is attacker-controlled.</b> The checks, in order,
    ///     and what each one stops:
    ///     </para>
    ///     <list type="number">
    ///         <item>The projectile id is looked up <i>scoped to the reporting session</i>, so a
    ///         client can only name projectiles the server spawned for it. An unknown id resolves to
    ///         nothing and the report is dropped.</item>
    ///         <item>The projectile must still carry <see cref="PredictedProjectileServerComponent"/>
    ///         and must not already be marked <c>Hit</c>. The latch is set before any collision is
    ///         applied, so one projectile can be claimed exactly once no matter how many reports
    ///         arrive for it - this is what stops a client replaying one report for repeated damage.
    ///         </item>
    ///         <item>The recorded shooter session must match the reporting session. Redundant with
    ///         the scoped lookup, and kept as defence in depth in case a projectile is ever
    ///         re-registered under a different session.</item>
    ///         <item>Targets the client names that do not resolve, or that lack lag compensation,
    ///         fixtures, physics or a transform, are skipped - a client cannot claim a hit on
    ///         something the server is not tracking positions for.</item>
    ///         <item>Each surviving target must have a hard, mask-matching fixture
    ///         (<see cref="TryGetBlockingFixture"/>), must not be excluded by the collision rules
    ///         physics would have applied (<see cref="IsCollisionPrevented"/> - cover, prone targets,
    ///         ignored entities, the shooter), and must be reachable in a straight line from the
    ///         projectile (<see cref="HasClearShot"/>). None of these are in the source; without them
    ///         a claimed hit skips every rule the engine's own contact filtering enforces.</item>
    ///         <item>Finally the overlap itself is re-derived by
    ///         <see cref="RMCLagCompensationSystem.Collides"/>, which rewinds the target to the tick
    ///         this session reported and tests the projectile's real fixtures against the target's.
    ///         Targets that fail are skipped individually, not the whole report, so a partially wrong
    ///         report still lands its true hits.</item>
    ///     </list>
    ///     <para>
    ///     The client's claimed position travels in the report but is never read. Path A - the design
    ///     this replaced - accepted it as a re-centring hint within a CVar-bounded tolerance; what is
    ///     trusted here is one clamped scalar, the reported tick, from which the server derives the
    ///     whole geometry itself.
    ///     </para>
    ///     <para>
    ///     The report is bounded: at most <see cref="MaxClaimedHitsPerReport"/> entries are considered
    ///     and each target is adjudicated at most once, because a penetrating projectile deliberately
    ///     un-spends itself and so cannot be relied on to stop a repeat claim.
    ///     </para>
    /// </remarks>
    private void ProcessPredictedHit(PredictedProjectileHitEvent ev, ICommonSession player)
    {
        if (!_predicted.TryGetValue((player.UserId, ev.Projectile), out var projectile))
            return;

        if (!_predictedProjectileServerQuery.TryComp(projectile, out var predictedProjectile) ||
            predictedProjectile.Hit)
        {
            return;
        }

        // Deviation: written as an explicit NetUserId comparison. The source compared
        // `Shooter?.UserId != player.UserId.UserId`, which relied on NetUserId's implicit conversion
        // to Guid plus a lifted nullable comparison - correct, but an implicit conversion is not
        // something that belongs on an ownership check.
        if (predictedProjectile.Shooter is not { } shooter || shooter.UserId != player.UserId)
            return;

        if (!_projectileQuery.TryComp(projectile, out var projectileComp) ||
            !_physicsQuery.TryComp(projectile, out var projectilePhysics))
        {
            return;
        }

        // Latch before adjudicating, not after: a projectile is spent by being claimed, whether or
        // not any of the claims turn out to be true.
        predictedProjectile.Hit = true;

        // A claim naming an unbounded number of targets is an unbounded amount of adjudication work
        // for one packet. A real bullet cannot overlap many things at once, so cap it. Entries past
        // the cap are dropped, not rejected - the shot is already spent either way.
        var budget = MaxClaimedHitsPerReport;

        // Dedupe. A penetrating projectile deliberately un-spends itself in ProjectileCollide (the
        // hardlight bow, and Goobstation's TryPenetrate), so ProjectileSpent cannot be relied on to
        // stop a second claim against the SAME victim from applying damage twice.
        var alreadyHit = new HashSet<EntityUid>();

        foreach (var (netEnt, clientPos) in ev.Hit)
        {
            if (budget-- <= 0)
                break;

            if (GetEntity(netEnt) is not { Valid: true } hit)
                continue;

            if (!alreadyHit.Add(hit))
                continue;

            // Hoisted out of Path A, which made this check itself. Path B does not: it unions raw
            // map-space AABBs, so two entities on different maps could be found to "overlap" at
            // coincidentally similar coordinates.
            if (!IsSameMap(projectile, hit))
                continue;

            if (!_lagCompensationQuery.TryComp(hit, out var otherLagComp) ||
                !_fixturesQuery.TryComp(hit, out var otherFixtures) ||
                !_physicsQuery.TryComp(hit, out var otherPhysics) ||
                !_transformQuery.TryComp(hit, out var otherTransform))
            {
                continue;
            }

            // Entities flagged as never-predictable are refused outright. Nothing else honoured this
            // component server-side, which made it inert.
            if (_ignorePredictionHitQuery.HasComp(hit))
                continue;

            // Fail CLOSED when no fixture of the target could have stopped this projectile. The
            // source falls through to Collides with an empty fixture set, which degenerates to a
            // point box that the AABB enlargement then inflates into a several-tile square - so a
            // target the bullet could never physically collide with got a LARGER acceptance area
            // than one it could. Requiring a hard, mask-matching fixture is what physics would have
            // required, and it is also the fixture PreventCollideEvent needs below.
            if (!TryGetBlockingFixture(projectilePhysics, otherFixtures, out var otherFixture))
                continue;

            // Re-derive the collision rules physics would have applied. ProjectileCollide is entered
            // AFTER the engine's contact filtering, so a predicted hit otherwise skips every rule
            // that filtering enforces: RequireProjectileTargetComponent (a prone target, or one
            // behind cover, is meant to be un-hittable), ProjectileComponent.IgnoredEntities,
            // IgnoreShooter, and embedding. Without this a client can shoot someone lying behind a
            // table by claiming a hit the server's own physics just refused.
            if (IsCollisionPrevented(projectile, projectilePhysics, hit, otherPhysics, otherFixture))
                continue;

            // A claimed hit must also have been reachable. Collides is purely a proximity test - it
            // asks whether the bullet is inside an enlarged box around the target - so on its own it
            // accepts a target standing to the SIDE of the bullet, behind a wall.
            if (!HasClearShot(projectile, hit, player))
                continue;

            // Path B. The target is rewound to the tick THIS session reported (clamped at the single
            // write point in SetLastRealTick), and the test is a real AABB-vs-AABB overlap between
            // the projectile's own fixtures and the target's, with MarginTiles of slack.
            //
            // The client's claimed position - clientPos - is deliberately not passed and not read.
            // Path A accepted it as a re-centring hint within a CVar tolerance; Path B derives the
            // whole geometry server-side from one clamped scalar instead, which is a strictly
            // smaller thing to trust.
            //
            // The same-map checks Path A performed internally are hoisted above, because Path B
            // compares raw map-space AABBs and does not make them itself.
            if (!_lagCompensation.Collides(
                    (hit, otherFixtures),
                    (projectile, projectilePhysics),
                    player,
                    _lagCompensation.GetLastRealSubstep(player.UserId)))
            {
                if (_logHits)
                    Log.Info("missed");

                continue;
            }

            if (_logHits)
                Log.Info("hit");

            // Deviation: Omu declares ProjectileCollide on the concrete server ProjectileSystem, not
            // on SharedProjectileSystem as RMC-14 does, so the dependency above is the concrete one.
            _projectile.ProjectileCollide((projectile, projectileComp, projectilePhysics), hit, true);
        }
    }

    public override void Update(float frameTime)
    {
        // The CVar gate for this path is at the point of receipt (OnPredictedProjectileHit): with
        // prediction off the buffer is always empty, so this drains nothing. The try/finally
        // guarantees the buffer is cleared even if one report throws, so a single bad report cannot
        // wedge the queue for everyone else.
        try
        {
            foreach (var ev in _predictedHits)
            {
                ProcessPredictedHit(ev.Event, ev.Player);
            }
        }
        finally
        {
            _predictedHits.Clear();
        }

        // Deliberately NOT gated on GunPrediction. These are projectiles that were already adjudicated
        // as hits and have travelled past their impact point; deleting them is pure cleanup with no
        // observable effect beyond not leaking. Gating it would strand every shot that was still in
        // flight at the moment the CVar was turned off.
        var predicted = EntityQueryEnumerator<PredictedProjectileHitComponent, TransformComponent>();
        while (predicted.MoveNext(out var uid, out var hit, out var xform))
        {
            var origin = hit.Origin;
            var coordinates = xform.Coordinates;
            if (!origin.TryDistance(EntityManager, _transform, coordinates, out var distance) ||
                distance >= hit.Distance)
            {
                QueueDel(uid);
            }
        }
    }
}

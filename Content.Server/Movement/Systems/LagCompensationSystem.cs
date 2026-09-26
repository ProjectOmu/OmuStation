// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Movement.Components;
using Content.Server._RMC14.Movement; // Omu - gun prediction port
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Movement.Systems;

/// <summary>
/// Stores a buffer of previous positions of the relevant entity.
/// Can be used to check the entity's position at a recent point in time.
/// </summary>
public sealed class LagCompensationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    // Omu - gun prediction port start
    // Was `public static readonly`. It is now an instance field so that
    // Content.Server/_RMC14/Movement/RMCLagCompensationSystem.cs can drive it from
    // `omu.lag_compensation_milliseconds`. No other file in the tree read the static.
    // I figured 500 ping is max, so 1.5 is 750.
    // Max ping I've had is 350ms from aus to spain.
    public TimeSpan BufferTime = TimeSpan.FromMilliseconds(750);

    [Dependency] private readonly RMCLagCompensationSystem _rmcLagCompensation = default!;
    // Omu end

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Info;
        SubscribeLocalEvent<LagCompensationComponent, MoveEvent>(OnLagMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var earliestTime = curTime - BufferTime;

        // Cull any old ones from active updates
        // Probably fine to include ignored.
        var query = AllEntityQuery<LagCompensationComponent>();

        while (query.MoveNext(out var comp))
        {
            while (comp.Positions.TryPeek(out var pos))
            {
                if (pos.Item1 < earliestTime)
                {
                    comp.Positions.Dequeue();
                    continue;
                }

                break;
            }
        }
    }

    private void OnLagMove(EntityUid uid, LagCompensationComponent component, ref MoveEvent args)
    {
        if (!args.NewPosition.EntityId.IsValid())
            return; // probably being sent to nullspace for deletion.

        component.Positions.Enqueue((_timing.CurTime, args.NewPosition, args.NewRotation));
    }

    public (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid, ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        if (pSession == null || !TryComp<LagCompensationComponent>(uid, out var lag) || lag.Positions.Count == 0)
            return (xform.Coordinates, xform.LocalRotation);

        var angle = Angle.Zero;
        var coordinates = EntityCoordinates.Invalid;

        // Omu - gun prediction port start
        // Was: rewind by `pSession.Ping * 1.5`, a round-trip estimate that has nothing to do with
        // what the client actually rendered. Rewind by the last server tick the client had
        // authoritative state for instead (plus the physics substep it was mid-way through).
        //
        // The depth is not computed here. Deriving it from a tick that was
        // only bounded when it was stored let the rewind grow by one tick per tick for as long as a
        // client chose not to report. GetRewindOffset re-applies the session's own bound at the
        // moment the rewind is spent, and owns the substep adjustment with it, so every consumer
        // gets the same answer.
        var offsetTime = _rmcLagCompensation.GetRewindOffset(pSession.UserId);

        // An over-long rewind degrades to no rewind at all rather than to a clamped one: we have no
        // position history that old, so pretending otherwise would be worse than not rewinding.
        // GetRewindOffset already caps at the same buffer; this stays as defence in depth for the
        // day the two are configured from different places.
        if (offsetTime > BufferTime || offsetTime < TimeSpan.Zero)
            offsetTime = TimeSpan.Zero;

        var sentTime = _timing.CurTime - offsetTime;

        // Take the *last* position recorded at the earliest timestamp at or after sentTime.
        // Upstream broke on the first entry instead, which for an entity that moved more than once
        // inside a single tick returned an intermediate position the client never saw; the tick's
        // final position is the one that got networked.
        TimeSpan? found = null;
        foreach (var pos in lag.Positions)
        {
            if (found != null && found != pos.Item1)
                break;

            coordinates = pos.Item2;
            angle = pos.Item3;

            if (pos.Item1 >= sentTime)
                found ??= pos.Item1;
        }
        // Omu end

        if (coordinates == default)
        {
            Log.Debug($"No long comp coords found, using {xform.Coordinates}");
            coordinates = xform.Coordinates;
            angle = xform.LocalRotation;
        }
        else
        {
            Log.Debug($"Actual coords is {xform.Coordinates} and got {coordinates}");
        }

        return (coordinates, angle);
    }

    public Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, session, xform);
        return angle;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, session, xform);
        return coordinates;
    }
}
// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Server/_RMC14/Weapons/Ranged/Prediction/GunPredictionSystem.cs (Collides/adjudication idea)
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using System.Numerics;
using Content.Server._RMC14.Movement;
using Content.Server.Movement.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Weapons.Ranged;

/// <summary>
///     Applies server-side lag compensation to hitscan shots.
/// </summary>
/// <remarks>
///     <para>
///     Hitscan raycasts run against <em>live</em> physics, so a player shooting a strafing target
///     misses by their round-trip time. Omu already stored the position history needed to fix that
///     (<see cref="LagCompensationComponent"/>) but only melee ever consumed it.
///     </para>
///     <para>
///     This re-adjudicates the shot against targets' rewound coordinates and corrects
///     <see cref="HitscanRaycastFiredData.HitEntity"/> before the damage/stun/trigger systems see it.
///     It is deliberately conservative and can only ever move a hit <em>closer</em> to the shooter:
///     </para>
///     <list type="bullet">
///       <item>only entities that actually moved since the shooter last saw them are candidates, so
///             lag compensation can never manufacture a hit on a stationary target the live raycast
///             legitimately missed;</item>
///       <item>the rewind depth is bounded by <c>omu.lag_compensation_milliseconds</c> and, per
///             session, by what its measured connection can justify — see
///             <c>SharedRMCLagCompensationSystem.GetRewindOffset</c>;</item>
///       <item>nothing past whatever the live raycast stopped on can be substituted for it, so a
///             correction cannot reach a target the live shot never got to;</item>
///       <item>the slop around a rewound hitbox is bounded by
///             <c>omu.lag_compensation_margin_tiles</c>;</item>
///       <item>the path from the shooter to the rewound position is re-tested against live geometry,
///             so a corrected hit can never travel through a wall.</item>
///     </list>
/// </remarks>
public sealed class HitscanLagCompensationSystem : EntitySystem
{
    [Dependency] private readonly RMCLagCompensationSystem _lagCompensation = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    ///     How far a rewound position must sit from the live one before we consider the entity to
    ///     have "moved". Matches the tolerance RMC-14 uses in <c>IsWithinMargin</c>.
    /// </summary>
    private const float MovedEpsilon = 0.01f;

    /// <summary>
    ///     Deliberately over-generous ceiling, in tiles per second, on how fast anything carrying a
    ///     <see cref="LagCompensationComponent"/> can move away from where it was rewound to. Used
    ///     only to size the broad reject in <see cref="TryFindRewoundTarget"/>; too large merely
    ///     costs a little work, too small would silently drop legitimate hits.
    /// </summary>
    /// <remarks>
    ///     A sprinting humanoid does about 4.5 tiles/s and the fastest ridden/piloted things in the
    ///     tree are still under 10. The history in <c>LagCompensationComponent.Positions</c> stores
    ///     <em>parent-relative</em> <c>EntityCoordinates</c> and is resolved against the parent's
    ///     <em>current</em> transform, so ordinary grid motion contributes nothing at all; only a
    ///     mid-window reparent (walking off a moving shuttle) can make the resolved rewound position
    ///     diverge faster than the entity itself moves. 50 is an order of magnitude over walking
    ///     speed and leaves room for that case.
    /// </remarks>
    private const float MaxRewindSpeedTilesPerSecond = 50f;

    /// <summary>
    ///     Ceiling, in tiles, on how far a hard fixture's AABB can extend from the entity's own
    ///     position. Ordinary mob fixtures are radius ~0.35; Lavaland bosses and mechs are the
    ///     largest things with a <see cref="LagCompensationComponent"/> and are well under this.
    /// </summary>
    private const float MaxHitboxHalfExtentTiles = 4f;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<RequireProjectileTargetComponent> _requireTargetQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _requireTargetQuery = GetEntityQuery<RequireProjectileTargetComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        // Before reflection: decide who was actually hit first, then let reflection rule on them.
        SubscribeLocalEvent<HitscanBasicRaycastComponent, AttemptHitscanRaycastFiredEvent>(
            OnAttemptRaycastFired,
            before: [typeof(HitscanReflectSystem)]);
    }

    private void OnAttemptRaycastFired(Entity<HitscanBasicRaycastComponent> ent, ref AttemptHitscanRaycastFiredEvent args)
    {
        if (args.Cancelled)
            return;

        // A reflected beam's "shooter" is whatever bounced it, not whoever pulled the trigger.
        // Rewinding from that entity's perspective would be meaningless, so leave reflections alone.
        if (CompOrNull<HitscanReflectComponent>(ent)?.CurrentReflections > 0)
            return;

        if (args.Data.Shooter is not { } shooter)
            return;

        // Only players have a perspective to rewind to.
        if (!_actorQuery.TryComp(shooter, out var actor))
            return;

        // Matches HitscanBasicRaycastSystem's special case: shooters inside a container use the raw
        // raycast result, so there is no pass-through rule for us to reproduce faithfully.
        if (_container.IsEntityOrParentInContainer(shooter))
            return;

        if (!_xformQuery.TryComp(shooter, out var shooterXform))
            return;

        var origin = _transform.GetMapCoordinates(shooter, shooterXform);
        if (origin.MapId == MapId.Nullspace)
            return;

        var lengthSquared = args.Data.ShotDirection.LengthSquared();
        if (lengthSquared <= 0f)
            return;

        var direction = args.Data.ShotDirection / MathF.Sqrt(lengthSquared);
        var session = actor.PlayerSession;
        var mask = (int) ent.Comp.CollisionMask;
        var margin = _lagCompensation.MarginTiles;

        if (!TryFindRewoundTarget(ent.Comp.MaxDistance,
                mask,
                margin,
                origin,
                direction,
                shooter,
                session,
                out var candidate,
                out var candidateDistance))
        {
            return;
        }

        if (candidate == args.Data.HitEntity)
            return;

        if (IsPathBlocked(origin, direction, candidateDistance, mask, shooter, candidate, args.Data.HitEntity))
            return;

        args.Data.HitEntity = candidate;
    }

    /// <summary>
    ///     Finds the nearest entity along the shot whose <em>rewound</em> hitbox the ray passes
    ///     through. Only entities whose rewound position differs from their live position are
    ///     considered — the live raycast already adjudicated everything else correctly.
    /// </summary>
    private bool TryFindRewoundTarget(float maxDistance,
        int mask,
        float margin,
        MapCoordinates origin,
        Vector2 direction,
        EntityUid shooter,
        ICommonSession session,
        out EntityUid target,
        out float distance)
    {
        target = default;
        distance = float.MaxValue;

        var ray = new Ray(origin.Position, direction);
        var found = false;

        // Broad reject box. An entity is only ever accepted below if the ray passes through its
        // rewound hard-fixture AABB enlarged by `margin`, i.e. if its REWOUND position lies within
        // (MaxHitboxHalfExtentTiles + margin) of the shot segment. Its LIVE position can differ from
        // the rewound one by at most one rewind window's worth of travel: LagCompensationSystem culls
        // history older than BufferTime and degrades to no rewind at all past it, and BufferTime is
        // driven from omu.lag_compensation_milliseconds - the same cvar MaxRewindTicks() reads, which
        // is why the window is derived here rather than hardcoded. So
        //     slack = MaxRewindSpeedTilesPerSecond * window + MaxHitboxHalfExtentTiles + margin
        // bounds the Euclidean distance from the segment to the live position of any entity that
        // could possibly be accepted, and a point within Euclidean distance d of a segment is always
        // inside that segment's AABB enlarged by d. Rejecting on this box therefore cannot change
        // which entity wins - it only skips the rewind and fixture work for entities that could not
        // have won anyway.
        var rewindWindow = (float) (_lagCompensation.MaxRewindTicks() * _timing.TickPeriod.TotalSeconds);
        var slack = MaxRewindSpeedTilesPerSecond * rewindWindow + MaxHitboxHalfExtentTiles + margin;
        var shotBounds = Box2.FromTwoPoints(origin.Position, origin.Position + direction * maxDistance)
            .Enlarged(slack);

        var query = EntityQueryEnumerator<LagCompensationComponent, FixturesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var lag, out var fixtures, out var xform))
        {
            // Cheap rejects first: every mob in the world carries a LagCompensationComponent, and
            // this runs on every shot.
            if (uid == shooter)
                continue;

            if (xform.MapID != origin.MapId)
                continue;

            // A stationary entity records nothing, so it can never need compensating.
            if (lag.Positions.Count == 0)
                continue;

            // Omu: an entity that is only hittable when deliberately aimed
            // at - a prone target, or one behind cover - must not be made hittable by the rewind.
            // The live raycast enforces this at HitscanBasicRaycastSystem by comparing against the
            // shooter's declared target, but that target is carried on HitscanTraceEvent and is not
            // present in HitscanRaycastFiredData, so this system cannot reproduce the comparison
            // from what it is given. It therefore refuses such candidates outright.
            //
            // This fails closed. The cost is that deliberately aiming at a prone target that then
            // moves gets no compensation, which is the same treatment it received before this
            // system existed. The alternative - accepting them - would let a shot land on a target
            // the live path had just declined, which is the projectile-side bypass that
            // GunPredictionSystem.IsCollisionPrevented exists to close; the two adjudicators should
            // not disagree about the same gameplay rule.
            if (_requireTargetQuery.TryComp(uid, out var requireTarget) && requireTarget.Active)
                continue;

            var live = _transform.GetMapCoordinates(uid, xform);
            if (!shotBounds.Contains(live.Position))
                continue;

            var rewound = _transform.ToMapCoordinates(_lagCompensation.GetCoordinates(uid, session, xform));
            if (rewound.MapId != origin.MapId)
                continue;

            // If it did not move, the live raycast's verdict already stands.
            if ((rewound.Position - live.Position).LengthSquared() <= MovedEpsilon * MovedEpsilon)
                continue;

            if (!TryGetHardBounds((uid, fixtures), rewound.Position, mask, out var bounds))
                continue;

            bounds = bounds.Enlarged(margin);

            if (!ray.Intersects(bounds, out var hitDistance, out _))
                continue;

            if (hitDistance > maxDistance || hitDistance >= distance)
                continue;

            found = true;
            target = uid;
            distance = hitDistance;
        }

        return found;
    }

    /// <summary>
    ///     Unions the AABBs of the hard fixtures of <paramref name="entity"/> that
    ///     <paramref name="mask"/> catches, evaluated at <paramref name="position"/>.
    ///     Mirrors the filtering <c>SharedPhysicsSystem.IntersectRay</c> does.
    /// </summary>
    private static bool TryGetHardBounds(Entity<FixturesComponent> entity, Vector2 position, int mask, out Box2 bounds)
    {
        bounds = new Box2(position, position);
        var any = false;
        var transform = new Transform(position, 0f);

        foreach (var fixture in entity.Comp.Fixtures.Values)
        {
            if (!fixture.Hard || (fixture.CollisionLayer & mask) == 0)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                bounds = any
                    ? bounds.Union(fixture.Shape.ComputeAABB(transform, i))
                    : fixture.Shape.ComputeAABB(transform, i);
                any = true;
            }
        }

        return any;
    }

    /// <summary>
    ///     Re-tests the path to a rewound position against live geometry. Walls do not move, so this
    ///     is what stops lag compensation from ever shooting through one.
    /// </summary>
    /// <param name="liveHit">
    ///     What the live raycast stopped on, or null if it stopped on nothing.
    /// </param>
    private bool IsPathBlocked(MapCoordinates origin,
        Vector2 direction,
        float distance,
        int mask,
        EntityUid shooter,
        EntityUid candidate,
        EntityUid? liveHit)
    {
        if (distance <= 0f)
            return false;

        var ray = new CollisionRay(origin.Position, direction, mask);
        foreach (var result in _physics.IntersectRay(origin.MapId, ray, distance, shooter, false))
        {
            if (result.HitEntity == candidate)
                continue;

            // Omu: whatever the live raycast stopped on blocks,
            // whatever it is. This is what makes "can only move a hit closer to the shooter" true
            // rather than merely usually true - nothing past the live hit is reachable, so nothing
            // past it can be substituted for it.
            //
            // It also closes the mirror of the candidate-side rule below. HitscanBasicRaycastSystem
            // passes through an active RequireProjectileTarget entity UNLESS it is the target the
            // shooter declared, in which case the ray stops there. This system is not given that
            // declared target, but it is given what the ray stopped on, which answers the same
            // question: without this check, a shooter could name a prone body, have the live ray
            // stop on it, and then have the hit moved past it onto a strafing mob behind - the exact
            // "a target the live path declined becomes the hit" that the candidate-side rule exists
            // to prevent, running in the opposite direction.
            if (liveHit != null && result.HitEntity == liveHit)
                return true;

            // Things you have to aim at specifically are not blockers; the live raycast passes
            // through them too. Only reached for entities the live ray did NOT stop on, per above.
            if (_requireTargetQuery.TryComp(result.HitEntity, out var requireTarget) && requireTarget.Active)
                continue;

            return true;
        }

        return false;
    }
}

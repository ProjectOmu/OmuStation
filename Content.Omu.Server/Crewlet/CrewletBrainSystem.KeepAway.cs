// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private const float RefugeMargin = 0.5f;
    private const float RefugeArrival = 0.25f;

    private static readonly float[] RefugeAngles =
    {
        0f, 0.5f, -0.5f, 1f, -1f, 1.5f, -1.5f, 2f, -2f, 2.5f, -2.5f, MathF.PI,
    };

    private bool WantsKeepAway(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (!ent.Comp.Scanned
            || !IsReady(ent.Comp, CrewletActivity.KeepAway, now)
            || NearestDisliked(ent) is not { } threat)
            return false;

        ent.Comp.Candidate = threat;
        return true;
    }

    private void RunKeepAway(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Start)
        {
            if (ent.Comp.Target is not { } threat || !Present(threat) || PickRefuge(ent, threat) is not { } refuge)
            {
                Fail(ent, now);
                return;
            }

            SetWalking(ent, true);
            SetCourse(ent, refuge, RefugeArrival, ent.Comp.MoveTimeout, now);
            Complain(ent, threat, now);
        }

        switch (Move(ent, now))
        {
            case CrewletMove.Moving:
                return;
            case CrewletMove.Arrived:
                End(ent);
                return;
            default:
                Fail(ent, now);
                return;
        }
    }

    private EntityUid? NearestDisliked(Entity<CrewletBrainComponent> ent)
    {
        if (!TryComp<CrewletReputationComponent>(ent, out var reputation))
            return null;

        EntityUid? nearest = null;
        var nearestDistance = ent.Comp.KeepAwayRange;
        foreach (var person in ent.Comp.People)
        {
            if (!Disliked(ent, reputation, person))
                continue;

            var distance = DistanceTo(ent, person);
            if (distance >= nearestDistance)
                continue;

            nearest = person;
            nearestDistance = distance;
        }

        return nearest;
    }

    private bool NearDisliked(Entity<CrewletBrainComponent> ent, Vector2 position)
    {
        if (!TryComp<CrewletReputationComponent>(ent, out var reputation))
            return false;

        foreach (var person in ent.Comp.People)
        {
            if (Disliked(ent, reputation, person)
                && (_transform.GetWorldPosition(person) - position).Length() < ent.Comp.KeepAwayRange)
                return true;
        }

        return false;
    }

    private bool Disliked(Entity<CrewletBrainComponent> ent, CrewletReputationComponent reputation, EntityUid person)
    {
        return !TerminatingOrDeleted(person)
            && _mobState.IsAlive(person)
            && !_container.IsEntityInContainer(person)
            && _reputation.GetOpinion((ent.Owner, reputation), person) <= reputation.WaryThreshold;
    }

    private EntityCoordinates? PickRefuge(Entity<CrewletBrainComponent> ent, EntityUid threat)
    {
        var xform = Transform(ent);
        var danger = _transform.GetWorldPosition(threat);
        var away = _transform.GetWorldPosition(xform) - danger;
        var heading = away.LengthSquared() > 0.0001f ? Vector2.Normalize(away) : Vector2.UnitX;
        var radius = ent.Comp.KeepAwayRange + RefugeMargin;

        foreach (var angle in RefugeAngles)
        {
            var spot = danger + new Angle(angle).RotateVec(heading) * radius;
            var coordinates = ToGridCoordinates(new MapCoordinates(spot, xform.MapID));
            if (_turf.GetTileRef(coordinates) is not { } tile
                || _turf.IsSpace(tile)
                || _turf.IsTileBlocked(tile, CollisionGroup.MobMask)
                || NearDisliked(ent, spot))
                continue;

            return coordinates;
        }

        return null;
    }

    private void SetWalking(EntityUid uid, bool walking)
    {
        if (!TryComp<InputMoverComponent>(uid, out var mover) || mover.Sprinting != walking)
            return;

        _mover.SetSprinting((uid, mover), 0, walking);
        if (!walking)
            mover.CurTickWalkMovement = Vector2.Zero;
    }
}

// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Server.NPC.Components;
using Robust.Shared.Map;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private enum CrewletMove : byte
    {
        Moving,
        Arrived,
        Failed,
        TimedOut,
    }

    private void SetCourse(Entity<CrewletBrainComponent> ent,
        EntityCoordinates destination,
        float range,
        TimeSpan timeout,
        TimeSpan now,
        CrewletStep step = CrewletStep.Approach)
    {
        StopMoving(ent);
        ent.Comp.Step = step;
        ent.Comp.Destination = destination;
        ent.Comp.MoveRange = range;
        ent.Comp.StepEnds = now + timeout;
    }

    private void Approach(Entity<CrewletBrainComponent> ent, EntityUid target, float range, TimeSpan now)
    {
        SetCourse(ent, new EntityCoordinates(target, Vector2.Zero), range, ent.Comp.MoveTimeout, now);
    }

    private CrewletMove Move(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var destination = ent.Comp.Destination;
        if (!destination.IsValid(EntityManager))
        {
            StopMoving(ent);
            return CrewletMove.Failed;
        }

        if (Transform(ent).Coordinates.TryDistance(EntityManager, _transform, destination, out var distance)
            && distance <= ent.Comp.MoveRange)
        {
            StopMoving(ent);
            return CrewletMove.Arrived;
        }

        if (PryingDoor(ent))
        {
            StopMoving(ent);
            if (ent.Comp.StepEnds < now + ent.Comp.DoorTryInterval)
                ent.Comp.StepEnds = now + ent.Comp.DoorTryInterval;

            return CrewletMove.Moving;
        }

        if (now >= ent.Comp.StepEnds)
        {
            StopMoving(ent);
            return CrewletMove.TimedOut;
        }

        if (!_steeringQuery.TryComp(ent, out var steering))
        {
            steering = _steering.Register(ent, destination);
            steering.Range = ent.Comp.MoveRange;
            return CrewletMove.Moving;
        }

        if (steering.Status == SteeringStatus.NoPath || !ClearWay(ent, steering, now))
        {
            StopMoving(ent);
            return CrewletMove.Failed;
        }

        if (steering.Status == SteeringStatus.InRange)
            steering.Status = SteeringStatus.Moving;

        if (!steering.Coordinates.Equals(destination))
            _steering.Register(ent, destination, steering);

        steering.Range = ent.Comp.MoveRange;
        return CrewletMove.Moving;
    }

    private EntityCoordinates ToGridCoordinates(MapCoordinates spot)
    {
        return _mapManager.TryFindGridAt(spot, out var grid, out _)
            ? _transform.ToCoordinates(grid, spot)
            : _transform.ToCoordinates(spot);
    }

    private void StopMoving(EntityUid uid)
    {
        if (_steeringQuery.TryComp(uid, out var steering))
            _steering.Unregister(uid, steering);
    }

    private float DistanceTo(EntityUid uid, EntityUid other)
    {
        var xform = Transform(uid);
        var otherXform = Transform(other);
        if (xform.MapID != otherXform.MapID)
            return float.PositiveInfinity;

        return (_transform.GetWorldPosition(xform) - _transform.GetWorldPosition(otherXform)).Length();
    }
}

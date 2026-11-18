// SPDX-License-Identifier: MIT

using Content.Server.NPC.Components;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.NPC;
using Content.Shared.Prying.Systems;
using Robust.Shared.Map.Components;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private bool ClearWay(Entity<CrewletBrainComponent> ent, NPCSteeringComponent steering, TimeSpan now)
    {
        var brain = ent.Comp;
        if (now < brain.NextDoorTry)
            return true;

        if (DoorAhead(steering) is not { } door)
        {
            brain.Door = null;
            return true;
        }

        if (DistanceTo(ent, door) > brain.InteractRange)
            return true;

        if (!TargetReady(brain, door, now))
            return false;

        brain.NextDoorTry = now + brain.DoorTryInterval;
        if (brain.Door != door)
        {
            brain.Door = door;
            brain.DoorTries = 0;
        }

        if (brain.DoorTries >= brain.MaxDoorTries)
        {
            brain.Door = null;
            Cooldown(ent, door, brain.DoorRetry, now);
            return false;
        }

        brain.DoorTries++;
        _interaction.InteractionActivate(ent, door);
        return true;
    }

    private EntityUid? DoorAhead(NPCSteeringComponent steering)
    {
        if (!steering.CurrentPath.TryPeek(out var poly)
            || (poly.Data.Flags & PathfindingBreadcrumbFlag.Door) == 0x0
            || !TryComp<MapGridComponent>(poly.GraphUid, out var grid))
            return null;

        foreach (var uid in _map.GetLocalAnchoredEntities(poly.GraphUid, grid, poly.Box))
        {
            if (_doorQuery.TryComp(uid, out var door) && door.State == DoorState.Closed)
                return uid;
        }

        return null;
    }

    private bool PryingDoor(EntityUid uid)
    {
        if (!TryComp<DoAfterComponent>(uid, out var doAfters))
            return false;

        foreach (var doAfter in doAfters.DoAfters.Values)
        {
            if (!doAfter.Cancelled && !doAfter.Completed && doAfter.Args.Event is DoorPryDoAfterEvent)
                return true;
        }

        return false;
    }
}

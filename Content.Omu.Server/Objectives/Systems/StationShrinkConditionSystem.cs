// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Components;
using Content.Server.Station.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Omu.Server.Objectives.Systems;

public sealed class StationShrinkConditionSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationShrinkConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<StationShrinkConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<StationShrinkConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (_station.GetOwningStation(args.Mind.OwnedEntity) is not { } station
            || _station.GetTileCount(station) == 0)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Station = station;
        ent.Comp.StartingTiles = _station.GetTileCount(station);
    }

    private void OnGetProgress(Entity<StationShrinkConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.Station is not { } station)
            return;

        var lost = ent.Comp.StartingTiles - _station.GetTileCount(station);
        args.Progress = Math.Clamp(lost / (ent.Comp.StartingTiles * ent.Comp.RequiredLoss), 0f, 1f);
    }
}

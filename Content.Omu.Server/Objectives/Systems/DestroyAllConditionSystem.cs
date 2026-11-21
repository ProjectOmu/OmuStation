// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Omu.Server.Objectives.Components;
using Content.Server.Station.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Objectives.Systems;

public sealed class DestroyAllConditionSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DestroyAllConditionComponent, RequirementCheckEvent>(OnRequirementCheck);
        SubscribeLocalEvent<DestroyAllConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<DestroyAllConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnRequirementCheck(Entity<DestroyAllConditionComponent> ent, ref RequirementCheckEvent args)
    {
        if (args.Cancelled)
            return;

        if (_station.GetOwningStation(args.Mind.OwnedEntity) is not { } station
            || !AnyOnStation(ent.Comp.Components, station))
        {
            args.Cancelled = true;
        }
    }

    private void OnAssigned(Entity<DestroyAllConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.Station = _station.GetOwningStation(args.Mind.OwnedEntity);
    }

    private void OnGetProgress(Entity<DestroyAllConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Station is { } station && !AnyOnStation(ent.Comp.Components, station) ? 1f : 0f;
    }

    private bool AnyOnStation(ComponentRegistry components, EntityUid station)
    {
        return components.Values.Any(entry => EntityManager.GetAllComponents(entry.Component.GetType())
            .Any(found => _station.GetOwningStation(found.Uid) == station));
    }
}

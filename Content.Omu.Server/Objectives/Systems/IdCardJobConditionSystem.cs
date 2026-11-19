// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Omu.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Objectives.Systems;

public sealed class IdCardJobConditionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardJobConditionComponent, ObjectiveGetProgressEvent>(OnJobGetProgress);
        SubscribeLocalEvent<TargetJobLostConditionComponent, ObjectiveGetProgressEvent>(OnTargetJobLostGetProgress);
    }

    private void OnJobGetProgress(Entity<IdCardJobConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetCurrentJobs(args.Mind).Any(ent.Comp.Jobs.Contains) ? 1f : 0f;
    }

    private void OnTargetJobLostGetProgress(Entity<TargetJobLostConditionComponent> ent,
        ref ObjectiveGetProgressEvent args)
    {
        if (!_target.GetTarget(ent, out var target)
            || !TryComp<MindComponent>(target, out var mind)
            || !_proto.Resolve(ent.Comp.Department, out var department))
        {
            return;
        }

        args.Progress = GetCurrentJobs(mind).Any(job => !department.Roles.Contains(job)) ? 1f : 0f;
    }

    private List<ProtoId<JobPrototype>> GetCurrentJobs(MindComponent mind)
    {
        var jobs = new List<ProtoId<JobPrototype>>();
        if (mind.OwnedEntity is not { } body || mind.CharacterName is not { } name)
            return jobs;

        var carried = _hands.EnumerateHeld(body).ToList();
        if (_inventory.TryGetSlotEntity(body, "id", out var slotItem))
            carried.Add(slotItem.Value);

        foreach (var item in carried)
        {
            if (_idCard.TryGetIdCard(item, out var idCard) && GetCardJob(idCard, name) is { } job)
                jobs.Add(job);
        }

        foreach (var station in _station.GetStations())
        {
            if (HasComp<StationRecordsComponent>(station)
                && _records.GetRecordByName(station, name) is { } id
                && _records.TryGetRecord<GeneralStationRecord>(new StationRecordKey(id, station), out var record)
                && record.JobPrototype != string.Empty)
            {
                jobs.Add(record.JobPrototype);
            }
        }

        return jobs;
    }

    private ProtoId<JobPrototype>? GetCardJob(Entity<IdCardComponent> idCard, string name)
    {
        if (TryComp<StationRecordKeyStorageComponent>(idCard, out var keyStorage)
            && keyStorage.Key is { } key
            && _records.TryGetRecord<GeneralStationRecord>(key, out var record))
        {
            return record.Name == name && record.JobPrototype != string.Empty ? record.JobPrototype : null;
        }

        return idCard.Comp.FullName == name ? idCard.Comp.JobPrototype : null;
    }
}

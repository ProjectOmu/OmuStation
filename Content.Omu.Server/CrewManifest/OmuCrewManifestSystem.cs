using Content.Server._Omu.CrewManifest;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Server.Silicons.StationAi;
using Content.Server.Station.Systems;
using Content.Shared.CrewManifest;
using Content.Shared.Mind;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared._DV.Silicons.Laws;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.CrewManifest;

/// <summary>
/// Omu-specific manifest additions for silicon crew that do not create normal station records.
/// </summary>
public sealed class OmuCrewManifestSystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> BorgJobId = "Borg";
    private static readonly ProtoId<JobPrototype> StationAiJobId = "StationAi";
    private static readonly ProtoId<NpcFactionPrototype> NanoTrasenFactionId = "NanoTrasen";

    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationAiSystem _stationAiSystem = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly JobSystem _jobSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CrewManifestEntriesCollectEvent>(OnCollectManifestEntries);
    }

    private void OnCollectManifestEntries(CrewManifestEntriesCollectEvent args)
    {
        var borgJob = _prototypeManager.Index(BorgJobId);
        var stationAiJob = _prototypeManager.Index(StationAiJobId);

        var borgQuery = EntityQueryEnumerator<BorgChassisComponent>();
        while (borgQuery.MoveNext(out var uid, out var chassis))
        {
            if (_stationSystem.GetOwningStation(uid) != args.Station)
                continue;

            if (chassis.BrainEntity == null)
                continue;

            if (!_npcFactionSystem.IsMember(uid, NanoTrasenFactionId))
                continue;

            if (!HasComp<SlavedBorgComponent>(uid))
                continue;

            if (!IsCrewSiliconMind(uid, BorgJobId, requireSlavedBorg: true))
                continue;

            args.Entries.Add((borgJob, BuildSiliconEntry(uid, borgJob)));
        }

        var aiQuery = EntityQueryEnumerator<StationAiCoreComponent>();
        while (aiQuery.MoveNext(out var uid, out var core))
        {
            if (_stationSystem.GetOwningStation(uid) != args.Station)
                continue;

            if (!_stationAiSystem.TryGetHeld((uid, core), out var held))
                continue;

            if (!IsCrewSiliconMind(held, StationAiJobId))
                continue;

            args.Entries.Add((stationAiJob, BuildSiliconEntry(held, stationAiJob)));
        }
    }

    private bool IsCrewSiliconMind(EntityUid uid, ProtoId<JobPrototype> expectedJob, bool requireSlavedBorg = false)
    {
        if (!_mindSystem.TryGetMind(uid, out var mindId, out _))
            return false;

        if (!_roleSystem.MindHasRole<SiliconBrainRoleComponent>(mindId))
            return false;

        if (_roleSystem.MindHasRole<SubvertedSiliconRoleComponent>(mindId))
            return false;

        if (requireSlavedBorg && !HasComp<SlavedBorgComponent>(uid))
            return false;

        if (_jobSystem.MindHasJobWithId(mindId, expectedJob))
            return true;

        return expectedJob == BorgJobId;
    }

    private CrewManifestEntry BuildSiliconEntry(EntityUid uid, JobPrototype job)
    {
        return new CrewManifestEntry(
            MetaData(uid).EntityName,
            job.LocalizedName,
            job.Icon.ToString(),
            job.ID);
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Omu.Common.CCVar;
using Content.Shared.Dataset;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

// todo it'd be nice to omumod this and while i can think of ways to pass jobproto and whatnot as string that seems so fucking ass i ended up not doing it.
namespace Content.Shared._Omu.Roles;

public sealed class JobAlternateTitleSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public static ProtoId<LocalizedDatasetPrototype> DatasetId(ProtoId<JobPrototype> job) => "AlternateTitles" + job.Id;

    public bool Enabled => _cfg.GetCVar(OmuCVars.AlternateJobTitles);

    public bool TryGetTitles(ProtoId<JobPrototype> job, [NotNullWhen(true)] out LocalizedDatasetValues? titles)
    {
        titles = _prototypes.TryIndex(DatasetId(job), out var dataset) ? dataset.Values : null;
        return titles != null;
    }

    public string? GetTitle(HumanoidCharacterProfile? profile, ProtoId<JobPrototype> job)
    {
        if (!Enabled || profile == null || !profile.JobAlternateTitles.TryGetValue(job, out var key))
            return null;

        return TryGetTitles(job, out var titles) && titles.Contains(key) ? Loc.GetString(key) : null;
    }
}

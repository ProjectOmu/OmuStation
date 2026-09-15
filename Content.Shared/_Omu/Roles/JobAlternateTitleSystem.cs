// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Omu.Common.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Omu.Roles;

public sealed class JobAlternateTitleSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public bool Enabled => _cfg.GetCVar(OmuCVars.AlternateJobTitles);

    public Dictionary<ProtoId<JobPrototype>, List<JobAlternateTitlePrototype>> GetTitlesByJob()
    {
        var byJob = new Dictionary<ProtoId<JobPrototype>, List<JobAlternateTitlePrototype>>();
        foreach (var title in _prototypes.EnumeratePrototypes<JobAlternateTitlePrototype>())
        {
            byJob.GetOrNew(title.Job).Add(title);
        }

        foreach (var titles in byJob.Values)
        {
            titles.Sort((a, b) => string.Compare(a.LocalizedName, b.LocalizedName, StringComparison.CurrentCulture));
        }

        return byJob;
    }

    public bool TryGetTitle(ProtoId<JobAlternateTitlePrototype>? titleId, ProtoId<JobPrototype> job, [NotNullWhen(true)] out JobAlternateTitlePrototype? title)
    {
        title = null;
        if (!Enabled || titleId == null)
            return false;

        if (!_prototypes.TryIndex(titleId.Value, out var found, false) || found.Job != job)
            return false;

        title = found;
        return true;
    }

    public bool TryGetTitle(HumanoidCharacterProfile? profile, ProtoId<JobPrototype> job, [NotNullWhen(true)] out JobAlternateTitlePrototype? title)
    {
        title = null;
        return profile != null
               && profile.JobAlternateTitles.TryGetValue(job, out var titleId)
               && TryGetTitle(titleId, job, out title);
    }

    public string GetJobTitle(HumanoidCharacterProfile? profile, JobPrototype job)
    {
        return TryGetTitle(profile, job.ID, out var title) ? title.LocalizedName : job.LocalizedName;
    }
}

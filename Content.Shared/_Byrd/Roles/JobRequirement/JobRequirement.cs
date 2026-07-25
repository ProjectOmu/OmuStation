using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Shared.Roles;

/// <summary>
/// Requires a character to be, or not be, a certain department
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class JobsRequirement : JobRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();

    public override bool Check(IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = new FormattedMessage();

        if (profile is null) //the profile could be null if the player is a ghost. In this case we don't need to block the role selection for ghostrole
            return true;

        var jobColors = new Dictionary<ProtoId<JobPrototype>, Color>();

        var depts = protoManager.EnumeratePrototypes<DepartmentPrototype>();
        foreach (var dept in depts)
        {
            foreach (var job in Jobs)
            {
                if (dept.Roles.Contains(job))
                    jobColors.Add(job, dept.Color);
            }
        }

        var sb = new StringBuilder();
        var prefix = "";
        foreach (var job in Jobs)
        {
            sb.Append(prefix);
            prefix = ", ";
            sb.Append($"[color={jobColors[job].ToHex()}]");
            sb.Append(Loc.GetString(protoManager.Index(job).Name));
            sb.Append("[/color]");
        }

        reason = FormattedMessage.FromMarkupPermissive($"{Loc.GetString("character-job-requirement", ("inverted", Inverted))}\n{sb}");

        foreach (var job in Jobs)
        {
            if (profile.JobPriorities.Any(job => Jobs.Contains(job.Key) && job.Value == JobPriority.High))
                return !Inverted;
        }

        return Inverted;
    }

    public override bool CheckJob(IEntityManager entManager,
        IPrototypeManager protoManager,
        JobPrototype job,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        var jobColors = new Dictionary<ProtoId<JobPrototype>, Color>();

        var depts = protoManager.EnumeratePrototypes<DepartmentPrototype>();
        foreach (var dept in depts)
        {
            foreach (var checkedJob in Jobs)
            {
                if (dept.Roles.Contains(checkedJob))
                    jobColors.Add(checkedJob, dept.Color);
            }
        }

        var sb = new StringBuilder();
        var prefix = "";
        foreach (var checkedJob in Jobs)
        {
            sb.Append(prefix);
            prefix = ", ";
            sb.Append($"[color={jobColors[checkedJob].ToHex()}]");
            sb.Append(Loc.GetString(protoManager.Index(checkedJob).Name));
            sb.Append("[/color]");
        }

        reason = FormattedMessage.FromMarkupPermissive($"{Loc.GetString("character-job-requirement", ("inverted", Inverted))}\n{sb}");

        if (Jobs.Contains(job)) return !Inverted;
        return Inverted;
    }
}
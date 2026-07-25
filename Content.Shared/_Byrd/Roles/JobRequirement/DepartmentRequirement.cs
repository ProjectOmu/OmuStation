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
public sealed partial class DepartmentRequirement : JobRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<DepartmentPrototype>> Departments = new();

    public override bool Check(IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = new FormattedMessage();

        if (profile is null) //the profile could be null if the player is a ghost. In this case we don't need to block the role selection for ghostrole
            return true;

        var sb = new StringBuilder();
        var prefix = "";
        foreach (var dept in Departments)
        {
            sb.Append(prefix);
            prefix = ", ";
            sb.Append($"[color={protoManager.Index(dept).Color.ToHex()}]");
            sb.Append(Loc.GetString(protoManager.Index(dept).Name));
            sb.Append("[/color]");
        }

        reason = FormattedMessage.FromMarkupPermissive($"{Loc.GetString("character-department-requirement", ("inverted", Inverted))}\n{sb}");

        foreach (var deptProto in Departments)
        {
            if (!protoManager.TryIndex(deptProto, out var dept))
                return false;

            if (profile.JobPriorities.Any(j => j.Value == JobPriority.High && dept.Roles.Contains(j.Key)))
                return !Inverted;
        }

        return Inverted;
    }

    public override bool CheckJob(IEntityManager entManager,
        IPrototypeManager protoManager,
        JobPrototype job,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        var sb = new StringBuilder();
        var prefix = "";
        foreach (var dept in Departments)
        {
            sb.Append(prefix);
            prefix = ", ";
            sb.Append($"[color={protoManager.Index(dept).Color.ToHex()}]");
            sb.Append(Loc.GetString(protoManager.Index(dept).Name));
            sb.Append("[/color]");
        }

        reason = FormattedMessage.FromMarkupPermissive($"{Loc.GetString("character-department-requirement", ("inverted", Inverted))}\n{sb}");

        foreach (var deptProto in Departments)
        {
            if (!protoManager.TryIndex(deptProto, out var dept))
                return false;

            if (dept.Roles.Contains(job))
                return !Inverted;
        }

        return Inverted;
    }
}

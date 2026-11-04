// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityTable;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Goobstation.Shared.SetSelector;

/// <summary>
/// A prototype that defines a set available for selection for <see>
///     <cref>SetSelectorComponent</cref>
/// </see>
/// </summary>
[Prototype]
public sealed partial class SelectableSetPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public string Name { get; private set; } = string.Empty;
    [DataField] public string Description { get; private set; } = string.Empty;
    [DataField] public SpriteSpecifier Sprite { get; private set; } = SpriteSpecifier.Invalid;
    [DataField] public List<EntProtoId> Content = new();
    [DataField] public List<ProtoId<EntityTablePrototype>> Tables = new();
    [DataField] public HashSet<JobRequirement> Requirements = new();

    public bool CanSelect(ICommonSession? session, ISharedPlaytimeManager playtime,
        IEntityManager entities, IPrototypeManager prototypes, out string? reason)
    {
        reason = null;
        if (Requirements.Count == 0)
            return true;

        if (session == null || !playtime.TryGetTrackerTimes(session, out var times))
        {
            reason = Loc.GetString("set-selector-playtime-loading");
            return false;
        }

        var allowed = JobRequirements.TryRequirementsMet(Requirements, times, out var failure, entities, prototypes, null);
        reason = failure?.ToString();
        return allowed;
    }
}

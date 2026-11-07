// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Shared.SetSelector;
using Content.Shared.Localizations;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.SetSelector;

public sealed partial class SetSelectorMenu
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly ISharedPlaytimeManager _playtime = default!;

    private void ApplyRequirements(SelectableSet child, SelectableSetInfo info)
    {
        var prototype = _prototypes.Index(info.Prototype);
        var allowed = prototype.CanSelect(_players.LocalSession, _playtime, _entities, _prototypes, out var reason);
        child.SetButton.Disabled = !allowed && !info.Selected;
        if (!allowed)
            child.Modulate = Color.Gray;
        if (prototype.Requirements.Count > 0)
        {
            child.MouseFilter = MouseFilterMode.Pass;
            var requirements = string.Join("\n", prototype.Requirements.OfType<DepartmentTimeRequirement>().Select(requirement =>
                Loc.GetString("set-selector-department-time",
                    ("time", ContentLocalizationManager.FormatPlaytime(requirement.Time)),
                    ("department", Loc.GetString(_prototypes.Index(requirement.Department).Name)))));
            child.ToolTip = $"{requirements}\n{reason ?? Loc.GetString("set-selector-playtime-met")}".Trim();
            child.SetButton.ToolTip = child.ToolTip;
        }
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.SetSelector;

public sealed partial class SelectableSetPrototype
{
    /// <summary>
    /// Requirements for selecting this set. Empty means unrestricted.
    /// </summary>
    [DataField] public HashSet<JobRequirement> Requirements = new();

    /// <summary>
    /// Checks the player's playtime against the requirements.
    /// Returns false if required playtime is unavailable.
    /// </summary>
    /// <param name="reason">The localized failure message, or null on success.</param>
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

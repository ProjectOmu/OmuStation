// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking.Events;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Spawning;

/// <summary>
///     Refuses a spawn when the character's selected traits are not ones they are allowed to have.
/// </summary>
/// <remarks>
///     <para>
///     The trait picker is clientside, so the client can be told a trait is unavailable and pick it anyway.
///     This is the serverside half of that check. It used to be 28 lines inlined into
///     <c>GameTicker.SpawnPlayer</c> along with two dependencies that only it used.
///     </para>
///     <para>
///     It hangs off <see cref="IsSpawnAllowedEvent"/> rather than <c>PlayerBeforeSpawnEvent</c>: handling that
///     event makes the ticker call <c>PlayerJoinGame</c>, which would take a refused player out of the lobby
///     and leave them with no body. See the remarks on <see cref="IsSpawnAllowedEvent"/>.
///     </para>
/// </remarks>
public sealed class TraitRestrictionSpawnSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;

    /// <summary>
    ///     Shown to a player whose character was refused over its traits.
    /// </summary>
    public const string RefusedMessage = "game-ticker-player-restricted-traits-selected-when-joining";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IsSpawnAllowedEvent>(OnIsSpawnAllowed);
    }

    private void OnIsSpawnAllowed(ref IsSpawnAllowedEvent ev)
    {
        // Somebody else already refused this spawn, no point re-deciding it (and no point overwriting their reason).
        if (ev.Cancelled)
            return;

        if (!IsTraitSelectionAllowed(ev.Player, ev.Profile))
            ev.Cancel(RefusedMessage);
    }

    /// <summary>
    ///     Whether <paramref name="profile"/>'s selected traits are ones <paramref name="player"/> may actually
    ///     take: every trait's requirements are met, and the selection is within the configured trait count and
    ///     global point budget.
    /// </summary>
    public bool IsTraitSelectionAllowed(ICommonSession player, HumanoidCharacterProfile profile)
    {
        var playTimes = _playTime.GetPlayTimes(player);

        var numSelectedTraits = 0;
        var traitPoints = _cfg.GetCVar(CCVars.TraitsDefaultPoints);

        foreach (var traitProtoId in profile.TraitPreferences)
        {
            var traitProto = _proto.Index(traitProtoId);
            traitPoints -= traitProto.GlobalCost;

            if (traitProto.CountsTowardsMaxTraits)
                numSelectedTraits++;

            // The trait exists, but this character is not allowed to have it.
            if (!JobRequirements.TryRequirementsMet(traitProto.Requirements,
                    playTimes,
                    out _,
                    EntityManager,
                    _proto,
                    profile))
            {
                return false;
            }
        }

        // More traits selected than they are allowed to select.
        var maxTraits = _cfg.GetCVar(CCVars.TraitsMaxTraits);
        if (numSelectedTraits > maxTraits && maxTraits >= 0)
            return false;

        // Over the global point budget.
        return !_cfg.GetCVar(CCVars.TraitsGlobalPointsEnabled) || traitPoints >= 0;
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Mind.Components;
using Content.Server.Silicons.Laws;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Server.GameStates;

namespace Content.Omu.Server.GameTicking;

/// <summary>
///     Fills in Omu's end-of-round silicon summary (the lawset a player's last mob was running, and
///     the entity to render it with) on the round-end scoreboard.
/// </summary>
/// <remarks>
///     This used to be inlined into <c>GameTicker.ShowRoundEndScoreboard</c>. It now hangs off
///     <see cref="RoundEndPlayerInfoEvent"/> so the upstream file stays fork-free.
/// </remarks>
public sealed class OmuRoundEndSiliconSummarySystem : EntitySystem
{
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly SiliconLawSystem _law = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndPlayerInfoEvent>(OnRoundEndPlayerInfo);
    }

    private void OnRoundEndPlayerInfo(RoundEndPlayerInfoEvent args)
    {
        var lastMob = TryComp<MindLastMobComponent>(args.MindId, out var lastMobComponent)
            ? lastMobComponent.LastMob
            : null;

        TryGetNetEntity(lastMob, out var borgPassEnt);

        SiliconLawset? lawset = null;
        if (lastMob != null && !TerminatingOrDeleted(lastMob))
        {
            if (TryComp<SiliconLawProviderComponent>(lastMob, out var providerComp))
                lawset = providerComp.Lawset ?? _law.GetLawset(providerComp.Laws);

            // The summary renders the last mob client-side, so it has to stay visible to everyone
            // after the round ends. Not gated on CCVars.RoundEndPVSOverrides, same as before this
            // moved out of GameTicker.
            //
            // Omu: gated on the lawset, not merely on the mob existing.
            // MindLastMobComponent is on the base mind prototype, so every mind that was ever in a
            // mob carries a last mob - overriding on that condition sent a global PVS override for
            // every player at round end, when RoundEndSummaryWindow only renders the silicon row,
            // and so only needs the entity, when `laws != null`. This is that exact condition.
            if (lawset != null)
                _pvsOverride.AddGlobalOverride(lastMob.Value);
        }

        args.Info.laws = lawset;
        args.Info.borgEnt = borgPassEnt;
    }
}

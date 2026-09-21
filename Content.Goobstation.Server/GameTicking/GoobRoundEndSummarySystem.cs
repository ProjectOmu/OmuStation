// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.LastWords;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Mind.Components;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Goobstation.Server.GameTicking;

/// <summary>
///     Fills in Goob Station's end-of-round screen data (last words, and how the player's last mob
///     ended up) on the round-end scoreboard.
/// </summary>
/// <remarks>
///     This used to be inlined into <c>GameTicker.ShowRoundEndScoreboard</c>. It now hangs off
///     <see cref="RoundEndPlayerInfoEvent"/> so the upstream file stays fork-free.
/// </remarks>
public sealed class GoobRoundEndSummarySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndPlayerInfoEvent>(OnRoundEndPlayerInfo);
    }

    private void OnRoundEndPlayerInfo(RoundEndPlayerInfoEvent args)
    {
        var lastWords = "";
        var mobState = MobState.Invalid;
        var damagePerGroup = new Dictionary<string, FixedPoint2>();

        var lastMob = TryComp<MindLastMobComponent>(args.MindId, out var lastMobComponent)
            ? lastMobComponent.LastMob
            : null;

        // Get last words if they exist (stored on the mind)
        if (TryComp<LastWordsComponent>(args.MindId, out var lastWordsComponent))
            lastWords = lastWordsComponent.LastWords;

        // Get mob state and damage if the mob still exists
        if (lastMob != null && !TerminatingOrDeleted(lastMob))
        {
            if (TryComp<MobStateComponent>(lastMob, out var mobStateComp))
                mobState = mobStateComp.CurrentState;

            if (TryComp<DamageableComponent>(lastMob, out var damageableComp))
                damagePerGroup = damageableComp.DamagePerGroup;
        }

        args.Info.LastWords = lastWords;
        args.Info.EntMobState = mobState;
        args.Info.DamagePerGroup = damagePerGroup;
    }
}

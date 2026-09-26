// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.RoundEnd; // Omu - RoundEndSystem now owns the restart announcement; replaces IChatManager
using Content.Shared.GameTicking.Components;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.GameTicking.Rules;

public sealed class MaxTimeRestartRuleSystem : GameRuleSystem<MaxTimeRestartRuleComponent>
{
    [Dependency] private readonly RoundEndSystem _roundEnd = default!; // Omu - replaces IChatManager; see RoundEndSystem.EndRound

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(RunLevelChanged);
    }

    protected override void Started(EntityUid uid, MaxTimeRestartRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if(GameTicker.RunLevel == GameRunLevel.InRound)
            RestartTimer(component);
    }

    protected override void Ended(EntityUid uid, MaxTimeRestartRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        StopTimer(component);
    }

    public void RestartTimer(MaxTimeRestartRuleComponent component)
    {
        // TODO FULL GAME SAVE
        component.TimerCancel.Cancel();
        component.TimerCancel = new CancellationTokenSource();
        Timer.Spawn(component.RoundMaxTime, () => TimerFired(component), component.TimerCancel.Token);
    }

    public void StopTimer(MaxTimeRestartRuleComponent component)
    {
        component.TimerCancel.Cancel();
    }

    private void TimerFired(MaxTimeRestartRuleComponent component)
    {
        // Omu - route through RoundEndSystem so that there is exactly one owner of the
        // round-restart timer (and so that RoundEndSystem state gets reset properly).
        // It dispatches the restart-ETA announcement itself.
        _roundEnd.EndRound(component.RoundEndDelay, Loc.GetString("rule-time-has-run-out"));
    }

    private void RunLevelChanged(GameRunLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<MaxTimeRestartRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var timer, out var gameRule))
        {
            // Omu - an inactive rule entity must skip to the next one, not abandon the sweep -
            // with two entities carrying this component, a later active one's timer was never stopped.
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            switch (args.New)
            {
                case GameRunLevel.InRound:
                    RestartTimer(timer);
                    break;
                case GameRunLevel.PreRoundLobby:
                case GameRunLevel.PostRound:
                    StopTimer(timer);
                    break;
            }
        }
    }
}
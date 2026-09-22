// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.RoundEnd; // Omu - RoundEndSystem now owns the restart announcement; replaces IChatManager
using Content.Shared.GameTicking.Components;
using Robust.Server.Player;
using Robust.Shared.Player;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.GameTicking.Rules;

public sealed class InactivityTimeRestartRuleSystem : GameRuleSystem<InactivityRuleComponent>
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!; // Omu - replaces IChatManager; see RoundEndSystem.EndRound

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(RunLevelChanged);
        _playerManager.PlayerStatusChanged += PlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= PlayerStatusChanged;
    }

    protected override void Ended(EntityUid uid, InactivityRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        StopTimer(uid, component);
    }

    public void RestartTimer(EntityUid uid, InactivityRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.TimerCancel.Cancel();
        component.TimerCancel = new CancellationTokenSource();
        Timer.Spawn(component.InactivityMaxTime, () => TimerFired(uid, component), component.TimerCancel.Token);
    }

    public void StopTimer(EntityUid uid, InactivityRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.TimerCancel.Cancel();
    }

    private void TimerFired(EntityUid uid, InactivityRuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Omu - route through RoundEndSystem so that there is exactly one owner of the
        // round-restart timer (and so that RoundEndSystem state gets reset properly).
        // It dispatches the restart-ETA announcement itself.
        _roundEnd.EndRound(component.RoundEndDelay, Loc.GetString("rule-time-has-run-out"));
    }

    private void RunLevelChanged(GameRunLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<InactivityRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var inactivity, out var gameRule))
        {
            // Omu - an inactive rule entity must skip to the next one, not abandon the sweep -
            // with two entities carrying this component, a later active one's timer was never stopped.
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            switch (args.New)
            {
                case GameRunLevel.InRound:
                    RestartTimer(uid, inactivity);
                    break;
                case GameRunLevel.PreRoundLobby:
                case GameRunLevel.PostRound:
                    StopTimer(uid, inactivity);
                    break;
            }
        }
    }

    private void PlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        var query = EntityQueryEnumerator<InactivityRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var inactivity, out var gameRule))
        {
            // Omu - an inactive rule entity must skip to the next one, not abandon the sweep -
            // with two entities carrying this component, a later active one's timer was never stopped.
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (GameTicker.RunLevel != GameRunLevel.InRound)
            {
                return;
            }

            if (_playerManager.PlayerCount == 0)
            {
                RestartTimer(uid, inactivity);
            }
            else
            {
                StopTimer(uid, inactivity);
            }
        }
    }
}
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.GameTicking.Rules;

public abstract partial class GameRuleSystem<T> : EntitySystem where T : IComponent
{
    [Dependency] protected readonly IRobustRandom RobustRandom = default!;
    [Dependency] protected readonly IChatManager ChatManager = default!;
    [Dependency] protected readonly GameTicker GameTicker = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    // Not protected, just to be used in utility methods
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly MapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<T, GameRuleAddedEvent>(OnGameRuleAdded);
        SubscribeLocalEvent<T, GameRuleStartedEvent>(OnGameRuleStarted);
        SubscribeLocalEvent<T, GameRuleEndedEvent>(OnGameRuleEnded);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
    }

    private void OnStartAttempt(RoundStartAttemptEvent args)
    {
        if (args.Forced || args.Cancelled)
            return;

        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out _, out var gameRule))
        {
            var minPlayers = gameRule.MinPlayers;
            var name = ToPrettyString(uid);

            if (args.Players.Length >= minPlayers)
                continue;

            if (gameRule.CancelPresetOnTooFewPlayers)
            {
                ChatManager.SendAdminAnnouncement(Loc.GetString("preset-not-enough-ready-players",
                    ("readyPlayersCount", args.Players.Length),
                    ("minimumPlayers", minPlayers),
                    ("presetName", name)));
                args.Cancel();
                //TODO remove this once announcements are logged
                Log.Info($"Rule '{name}' requires {minPlayers} players, but only {args.Players.Length} are ready.");
            }
            else
            {
                ForceEndSelf(uid, gameRule);
            }
        }
    }

    private void OnGameRuleAdded(EntityUid uid, T component, ref GameRuleAddedEvent args)
    {
        if (!TryComp<GameRuleComponent>(uid, out var ruleData))
            return;
        Added(uid, component, ruleData, args);
    }

    private void OnGameRuleStarted(EntityUid uid, T component, ref GameRuleStartedEvent args)
    {
        if (!TryComp<GameRuleComponent>(uid, out var ruleData))
            return;
        Started(uid, component, ruleData, args);
    }

    private void OnGameRuleEnded(EntityUid uid, T component, ref GameRuleEndedEvent args)
    {
        if (!TryComp<GameRuleComponent>(uid, out var ruleData))
            return;
        Ended(uid, component, ruleData, args);
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        var query = AllEntityQuery<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryComp<GameRuleComponent>(uid, out var ruleData))
                continue;

            AppendRoundEndText(uid, comp, ruleData, ref ev);
        }
    }

    /// <summary>
    /// Called when the gamerule is added.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This does not mean the round has started.</b> Preset rules are added from
    /// <c>AddGamePresetRules()</c>, which runs inside <c>LoadMaps()</c> during <b>map preload</b> —
    /// see <c>GameTicker.RoundFlow.cs</c> (<c>LoadMaps</c>) and <c>GameTicker.GamePreset.cs</c>
    /// (<c>AddGamePresetRules</c>). Preload begins <c>RoundPreloadTime</c> (20 seconds, see
    /// <c>GameTicker.Lobby.cs</c>) before the round starts, while <c>RunLevel</c> is still
    /// <see cref="GameRunLevel.PreRoundLobby"/>.
    /// </para>
    /// <para>
    /// So for those ~20 seconds <c>CurrentPreset</c> is non-null but there is no round, no station,
    /// and no spawned players. An <see cref="Added"/> handler must not assume any of them exist.
    /// If your logic needs a live round, guard it:
    /// <code>if (GameTicker.RunLevel != GameRunLevel.InRound) return;</code>
    /// as <c>AntagSelectionSystem</c> does, and defer the work to <see cref="Started"/> instead.
    /// </para>
    /// </remarks>
    protected virtual void Added(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {

    }

    /// <summary>
    /// Called when the gamerule begins.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Added"/>, this runs once the rule is actually started, so player-facing
    /// logic belongs here. Note a rule can still be started mid-round.
    /// </remarks>
    protected virtual void Started(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {

    }

    /// <summary>
    /// Called when the gamerule ends
    /// </summary>
    protected virtual void Ended(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {

    }

    /// <summary>
    /// Called at the end of a round when text needs to be added for a game rule.
    /// </summary>
    protected virtual void AppendRoundEndText(EntityUid uid, T component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {

    }

    /// <summary>
    /// Called on an active gamerule entity in the Update function
    /// </summary>
    protected virtual void ActiveTick(EntityUid uid, T component, GameRuleComponent gameRule, float frameTime)
    {

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<T, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp1, out var comp2))
        {
            if (!GameTicker.IsGameRuleActive(uid, comp2))
                continue;

            ActiveTick(uid, comp1, comp2, frameTime);
        }
    }
}
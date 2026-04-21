using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Common.StationEvent.Metrics;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Omu.Common.CCVar;
using Content.Omu.Server.GameDirector.Components;
using Content.Omu.Server.GameDirector.Metric;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.GameDirector;

/// <summary>
///   A scheduler which tries to keep station chaos within a set bound over time with the most suitable
///   good or bad events to nudge it in the correct direction.
/// </summary>
[UsedImplicitly]
public sealed partial class GameDirectorSystem : GameRuleSystem<GameDirectorComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EventManagerSystem _event = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    private ISawmill _sawmill = default!;
    private int _gameDirectorDebugPlayerCount;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("game_rule");
        SubscribeLocalEvent<GameDirectorComponent, EntityUnpausedEvent>(OnUnpaused);
        Subs.CVar(_configManager, GoobCVars.GameDirectorDebugPlayerCount, x => _gameDirectorDebugPlayerCount = x, true);
    }

    private static void OnUnpaused(EntityUid uid, GameDirectorComponent scheduler, ref EntityUnpausedEvent args)
    {
        scheduler.BeatStart += args.PausedTime;
        scheduler.TimeNextEvent += args.PausedTime;
    }

    protected override void Added(EntityUid uid, GameDirectorComponent scheduler, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        _sawmill.Info($"Game Director Spawned at {uid}");
        TrySpawnRoundstartAntags(scheduler); // Roundstart antags need to be selected in the lobby
        if(TryComp<SelectedGameRulesComponent>(uid,out var selectedRules))
            SetupEvents(scheduler, CountActivePlayers(), selectedRules);
        else
            SetupEvents(scheduler, CountActivePlayers());
    }

    /// <summary>
    ///   Decide what event to run next
    /// </summary>
    protected override void ActiveTick(EntityUid uid, GameDirectorComponent scheduler, GameRuleComponent gameRule, float frameTime)
    {
        var currTime = _timing.CurTime;
        // wait until it is time to consider a new event
        if (currTime < scheduler.TimeNextEvent)
            return;

        var chaos = CalculateChaos(uid);
        scheduler.CurrentChaos = chaos;
        LogMessage($"Chaos is: {chaos}");

        if (scheduler.Stories is not { Length: > 0 })
        {
            // no stories means this is a debug/metrics-only director, nothing to schedule
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        var count = CountActivePlayers();

        // figure out which story beat we are in (sets the chaos goals for event selection)
        var beat = DetermineNextBeat(scheduler, chaos, count);

        // TimeNextEvent == Zero means the director just started this round
        // we wait a bit before firing the first event so the round has time to settle
        if (scheduler.TimeNextEvent == TimeSpan.Zero)
        {
            var minimumTimeUntilFirstEvent = _configManager.GetCVar(GoobCVars.MinimumTimeUntilFirstEvent) / _event.EventSpeedup;
            scheduler.TimeNextEvent = _timing.CurTime + TimeSpan.FromSeconds(minimumTimeUntilFirstEvent);
            LogMessage($"Started, first event in {minimumTimeUntilFirstEvent} seconds");
            return;
        }

        RankedEvent? chosenEvent = null;
        // score all possible events and keep the ones that move chaos toward the beat goal
        var bestEvents = ChooseEvents(scheduler, beat, chaos, count);

        if (bestEvents.Count > 0)
        {
            // pick semi-randomly from the top candidates so the director is not fully deterministic
            // RandomEventLimit = 1 always picks the best, higher values add more variety
            chosenEvent = SelectBest(bestEvents, beat.RandomEventLimit);

            _event.RunNamedEvent(chosenEvent.PossibleEvent.StationEvent);
        }

        if (chosenEvent != null)
        {
            // wait between EventDelayMin and EventDelayMax before considering the next event
            scheduler.TimeNextEvent = currTime + TimeSpan.FromSeconds(_random.NextFloat(beat.EventDelayMin, beat.EventDelayMax) / _event.EventSpeedup);
        }
        else
        {
            // nothing ran this tick, retry sooner in case chaos or beat changes
            LogMessage($"Chaos is: {chaos} (No events ran)", false);
            scheduler.TimeNextEvent = currTime + TimeSpan.FromSeconds(scheduler.NoEventRetryDelay);
        }
    }

    private void LogMessage(string message, bool showChat=true)
    {
        // TODO: LogMessage strings all require localization.
        _adminLogger.Add(LogType.GameDirector, showChat?LogImpact.Medium:LogImpact.High, $"{message}");
        if (showChat)
            _chat.SendAdminAnnouncement("GameDirector " + message);
    }

    public ChaosMetrics CalculateChaos(EntityUid uid)
    {
        // ask every metric component on this entity to report its chaos score
        var calcEvent = new CalculateChaosEvent(new ChaosMetrics());
        RaiseLocalEvent(uid, ref calcEvent);

        var metrics = calcEvent.Metrics;

        // Combat = Friend + Hostile. Friend is negative when the crew is strong,
        // so Combat < 0 means the crew is winning, > 0 means the station is losing
        metrics.ChaosDict[ChaosMetric.Combat] = metrics.ChaosDict.GetValueOrDefault(ChaosMetric.Friend) +
                                                metrics.ChaosDict.GetValueOrDefault(ChaosMetric.Hostile);
        return calcEvent.Metrics;
    }
}

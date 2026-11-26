using System.Linq;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Goobstation.Shared.StationEvents;
using Content.Omu.Common.CCVar;
using Content.Omu.Server.StationEvents.SecretDoublePlus.ChaosFactors;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Tag;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus;

public sealed class SelectedEvent(
    EntityPrototype proto,
    float weight,
    float chaosScore,
    int minPlayers)
{
    public readonly EntityPrototype Proto = proto;
    public readonly float Weight = weight;
    public readonly float ChaosScore = chaosScore;
    public readonly int MinPlayers = minPlayers;
}

[UsedImplicitly]
public sealed class SecretDoublePlusSystem : GameRuleSystem<SecretDoublePlusComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly EventManagerSystem _event = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    // cvars
    private float _minimumTimeUntilFirstEvent;
    private float _roundstartChaosScoreMultiplier;
    private bool _verboseGameDirectorChatLogs;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _log.GetSawmill("secret_double_plus");

        Subs.CVar(_cfg, GoobCVars.MinimumTimeUntilFirstEvent, value => _minimumTimeUntilFirstEvent = value, true);
        Subs.CVar(_cfg, GoobCVars.RoundstartChaosScoreMultiplier, value => _roundstartChaosScoreMultiplier = value, true);
        Subs.CVar(_cfg, OmuCVars.VerboseGameDirectorChatLogs, value => _verboseGameDirectorChatLogs = value, true);

        // Clean up our metrics on shutdown
        SubscribeLocalEvent<SecretDoublePlusComponent, ComponentShutdown>(ResetMetrics);
    }

    protected override void Added(EntityUid uid, SecretDoublePlusComponent scheduler, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        // Set initial chaos score by player count
        var totalPlayers = GetTotalPlayerCount(_playerManager.Sessions);
        scheduler.ChaosScore = -1 *
            _random.NextFloat(scheduler.MinStartingChaos * totalPlayers, scheduler.MaxStartingChaos * totalPlayers) *
            _roundstartChaosScoreMultiplier;

        // Roll chaos change variation, biased towards 1.0
        var variationRoll = MathF.Pow(_random.NextFloat(), scheduler.ChaosChangeVariationExponent);
        var bias = _random.Prob(0.5f) ? scheduler.ChaosChangeVariationMin : scheduler.ChaosChangeVariationMax;

        scheduler.ChaosChangeVariation = MathHelper.Lerp(1f, bias, variationRoll);
        LogMessage($"Using chaos change multiplier of {scheduler.ChaosChangeVariation}");

        // Setup chaos factors
        foreach (var factor in scheduler.ChaosFactors)
        {
            factor.Initialize();
        }

        // Spawn roundstart antags
        LogMessage($"Initial chaos score: {scheduler.ChaosScore} (based on {totalPlayers} total players)");
        TrySpawnRoundstartAntags(scheduler);
    }

    protected override void ActiveTick(EntityUid uid, SecretDoublePlusComponent scheduler, GameRuleComponent gameRule, float frameTime)
    {
        SecretDoublePlusMetrics.ChaosScore.Set(scheduler.ChaosScore);

        var ramping = GetRamping(scheduler);
        var speedup = _event.EventSpeedup;
        var variation = scheduler.ChaosChangeVariation;

        foreach (var factor in scheduler.ChaosFactors)
        {
            scheduler.ChaosScore += factor.CalculateTimeAdjustedChaos(new ChaosContext(frameTime, ramping)) * speedup * variation;
        }

        var curTime = _timing.CurTime;
        if (curTime < scheduler.TimeNextEvent)
            return;

        // This is the first event, add an automatic delay
        if (scheduler.TimeNextEvent == TimeSpan.Zero)
        {
            var time = _minimumTimeUntilFirstEvent;
            scheduler.TimeNextEvent = curTime + TimeSpan.FromSeconds(time);
            LogMessage($"Started, first event in {time} seconds");
            return;
        }

        var amt = TimeSpan.FromSeconds(_random.NextDouble(scheduler.EventIntervalMin.TotalSeconds, scheduler.EventIntervalMax.TotalSeconds) / ramping / speedup);
        scheduler.TimeNextEvent = curTime + amt;

        LogMessage($"Chaos score: {scheduler.ChaosScore}, Next event at: {_ticker.RoundDuration() + amt} (ramping {ramping})");

        if (ShouldSkipTick(scheduler))
        {
            SecretDoublePlusMetrics.SkippedEvents.Inc();
            LogMessage("Skipping event tick");
            return;
        }

        // We multiply the target chaos by a percentage, where the SaveChaosBudgetRange
        // determines how much chaos budget we should hold back for the next active tick
        var chaosDampening = 1f;
        if (scheduler.ChaosScore < 0)
            chaosDampening = GetSavedChaosBudgetMultiplier(scheduler);

        var targetChaos = -scheduler.ChaosScore * chaosDampening;

        var possibleEvents = GetPossibleEvents(uid, scheduler);
        var weights = BuildChaosWeightedDict(scheduler, possibleEvents, targetChaos, scheduler.MidRoundChaosMatchingPower);

        if (weights.Count == 0)
            return;

        var selected = PickAndLog(weights, targetChaos);
        StartRule(scheduler, selected);
    }

    private void TrySpawnRoundstartAntags(SecretDoublePlusComponent scheduler)
    {
        if (scheduler.NoRoundstartAntags)
            return;

        // Build weight list for primary antag
        var primaryWeights = _prototypeManager.Index(scheduler.PrimaryAntagsWeightTable).Weights.ToDictionary();
        var primaryEvents = BuildSelectedEventsFromDict(primaryWeights);
        var primaryTargetChaos = -scheduler.ChaosScore * GetSavedChaosBudgetMultiplier(scheduler);

        // Build weight for secondary round start antags
        var secondaryWeights = _prototypeManager.Index(scheduler.RoundStartAntagsWeightTable).Weights.ToDictionary();
        var secondaryEvents = BuildSelectedEventsFromDict(secondaryWeights);

        // Add antags until we get above 0 chaos score
        const int iterationGuard = 50; // Cap our iteration count just in-case
        var primarySelected = false;
        for (var i = 0; scheduler.ChaosScore < 0 && i < iterationGuard; i++)
        {
            var events = primaryEvents;
            var targetChaos = primaryTargetChaos;
            if (primarySelected)
            {
                events = secondaryEvents;
                targetChaos = -scheduler.ChaosScore * GetSavedChaosBudgetMultiplier(scheduler);
            }

            var weights = BuildChaosWeightedDict(scheduler, events, targetChaos, scheduler.RoundStartChaosMatchingPower);
            if (weights.Count == 0)
                return;

            var pick = PickAndLog(weights, targetChaos);
            var isLoneSpawn = IsLoneSpawn(scheduler, pick);

            // We only ever try to select a given rule once across all iterations, even if it fails
            if (!scheduler.IgnoreIncompatible)
            {
                primaryEvents.RemoveAll(e => e.Proto.ID == pick.Proto.ID);
                secondaryEvents.RemoveAll(e => e.Proto.ID == pick.Proto.ID);
            }

            if (GameTicker.ReadyPlayerCount() < pick.MinPlayers)
                continue;

            // If primarySelected is true we've already spawned at least one thing, so a loneSpawn is invalid
            if (primarySelected && isLoneSpawn)
                continue;

            StartRule(scheduler, pick, true);
            primarySelected = true;

            // Since we only select a primary antag once, we only remove incompatible antags from the secondary list
            if (!scheduler.IgnoreIncompatible && _prototypeManager.TryIndex(pick.Proto.ID, out IncompatibleGameModesPrototype? incompatible))
            {
                secondaryEvents = secondaryEvents
                    .Where(w => !incompatible.Modes.Contains(w.Proto.ID))
                    .ToList();
            }

            // If we've run out of antags or picked a lone spawn we bail early and stop spawning antags
            if (secondaryEvents.Count == 0 || isLoneSpawn)
                return;
        }
    }

    private void StartRule(SecretDoublePlusComponent scheduler, SelectedEvent selected, bool shouldScale=false)
    {
        SecretDoublePlusMetrics.EventSelected.WithLabels(selected.Proto.ID).Inc();

        // We add the rule and let the ticker start it later
        var ruleId = _ticker.AddGameRule(selected.Proto.ID);
        var chaos = selected.ChaosScore;

        // When shouldScale is true we scale down the antag count for the rule if it would overshoot and bring us above 0 chaos
        if (shouldScale && chaos + scheduler.ChaosScore > 0 && TryComp<AntagSelectionComponent>(ruleId, out var selection))
        {
            var scale = Math.Clamp(-scheduler.ChaosScore / chaos, 0, 1);
            _antagSelection.ScaleAntagDefinitionCounts((ruleId, selection), scale);

            // We updated the rule so we need to update the corresponding chaos
            chaos = GetChaosScore(selected.Proto, selected.ChaosScore, scale);
            LogMessage($"Scaling selected rule {selected.Proto.ID} by {scale}, adjusted chaos score {chaos}", _verboseGameDirectorChatLogs);
        }

        scheduler.ChaosScore += chaos;
    }

    private List<SelectedEvent> BuildSelectedEventsFromDict(Dictionary<string, float> weights)
    {
        var selectedEvents = new List<SelectedEvent>();
        foreach (var (name, weight) in weights)
        {
            if (!_prototypeManager.TryIndex(name, out var entProto))
                continue;

            if (!entProto.TryGetComponent<GameRuleComponent>(out var ruleComp, _factory))
                continue;

            var chaos = GetChaosScore(entProto, ruleComp.ChaosScore);
            selectedEvents.Add(new SelectedEvent(entProto, weight, chaos, ruleComp.MinPlayers));
        }

        return selectedEvents;
    }

    private List<SelectedEvent> GetPossibleEvents(EntityUid uid, SecretDoublePlusComponent scheduler)
    {
        // Get all currently available events
        int? playerCountOverride = scheduler.IgnoreTimings ? int.MaxValue : null;
        TimeSpan? currentTimeOverride = scheduler.IgnoreTimings ? TimeSpan.MaxValue : null;
        var available = _event.AvailableEvents(
            scheduler.IgnoreTimings,
            playerCountOverride,
            currentTimeOverride,
            1f / GetRamping(scheduler)
        );

        // If we have SelectedGameRules we take an intersect of those and the available events
        var possibleEvents = available;
        if (TryComp<SelectedGameRulesComponent>(uid, out var selectedRules))
        {
            if (_event.TryBuildLimitedEvents(selectedRules.ScheduledGameRules, available, out var limitedEvents))
                possibleEvents = limitedEvents;
            else
                _sawmill.Error("SelectedRules was found but TryBuildLimitedEvents failed, falling back to all events");
        }

        // Construct the final list of Selected Events
        var result = new List<SelectedEvent>();
        foreach (var (proto, eventComp) in possibleEvents)
        {
            if (scheduler.DisallowedEvents.Contains(eventComp.EventType))
                continue;

            if (!proto.TryGetComponent<GameRuleComponent>(out var ruleComp, _factory))
                continue;

            var chaos = GetChaosScore(proto, ruleComp.ChaosScore);

            result.Add(new SelectedEvent(proto, eventComp.Weight, chaos, ruleComp.MinPlayers));
        }

        return result;
    }

    private Dictionary<SelectedEvent, float> BuildChaosWeightedDict(SecretDoublePlusComponent scheduler, List<SelectedEvent> events, float targetChaos, float matchingPower)
    {
        if (targetChaos == 0)
            return new Dictionary<SelectedEvent, float>();

        var adjustedWeights = new Dictionary<SelectedEvent, float>();
        foreach (var selected in events)
        {
            // Negative weights crash the pick function
            if (selected.Weight < 0)
                continue;

            // We adjust each event's weight down by checking how far away from the target score it is
            var difference = MathF.Abs(targetChaos - selected.ChaosScore) / MathF.Abs(targetChaos);
            var rawFitScore = 1f / (1f + difference);
            var fitScore = MathF.Pow(rawFitScore, matchingPower);

            // For a target chaos of 50, -50 and 150 are equidistant.
            // Picking 150 is likely better for the round than picking -50 and moving
            // our chaos score in the wrong direction.
            // Thus, we penalize events with flipped signs by a configurable amount.
            if (targetChaos * selected.ChaosScore < 0)
                fitScore *= scheduler.OppositeDirectionEventChance;

            adjustedWeights[selected] = selected.Weight * fitScore;
        }

        LogWeightedDict(adjustedWeights);
        if (!ValidateChaosWeightedDict(adjustedWeights, targetChaos))
            return new Dictionary<SelectedEvent, float>();

        return adjustedWeights;
    }

    private bool ValidateChaosWeightedDict(Dictionary<SelectedEvent, float> weights, float targetChaos)
    {
        var weightCount = weights.Count;
        if (weightCount == 0)
            return true;

        var weightSum = weights.Values.Sum();
        if (weightSum > 0)
            return true;

        _sawmill.Warning($"Chaos Weighted Dict Returned Negative or Zero sum, skipping. " +
                         $"target_chaos: {targetChaos} " +
                         $"weight_sum: {weightSum} " +
                         $"weight_count: {weightCount}"
        );

        return false;
    }

    private float GetChaosScore(EntityPrototype ruleProto, float? defaultChaos, float playerCountScale=1)
    {
        if (!ruleProto.TryGetComponent<AntagSelectionComponent>(out var selection, _factory))
            return defaultChaos ?? 0;

        var score = 0f;

        var players = (int) MathF.Round(playerCountScale * GetTotalPlayerCount(_playerManager.Sessions));
        foreach (var def in selection.Definitions)
        {
            if (def.ChaosScore == null)
                continue;

            var count = _antagSelection.GetTargetAntagCount((EntityUid.Invalid, selection), players, def);
            score += def.ChaosScore.Value * count;
        }

        if (score > 0)
            return score;

        return defaultChaos ?? 0;
    }

    private bool ShouldSkipTick(SecretDoublePlusComponent scheduler)
    {
        if (scheduler.MidRoundSkipChance <= 0f)
            return false;

        var quotient = MathF.Abs(scheduler.ChaosScore) / scheduler.MidRoundSkipRange;
        var halvings = MathF.Max(0, quotient - 1);

        var skipChance = MathF.Min(1f, MathF.Pow(0.5f, halvings) * scheduler.MidRoundSkipChance);
        return _random.Prob(skipChance);
    }

    private bool IsLoneSpawn(SecretDoublePlusComponent scheduler, SelectedEvent selected)
    {
        if (scheduler.IgnoreIncompatible)
            return false;

        if (!selected.Proto.TryGetComponent<TagComponent>(out var tag, _factory))
            return false;

        ProtoId<TagPrototype> loneSpawnTag = "LoneRunRule";
        return _tag.HasTag(tag, loneSpawnTag);
    }

    private SelectedEvent PickAndLog(Dictionary<SelectedEvent, float> weights, float? targetChaos = null)
    {
        var selected = _random.Pick(weights);

        var weightSum = weights.Values.Sum();
        LogMessage(
            $"Attempting to run {selected.Proto.ID} \n" +
            $"| Chaos Value: {selected.ChaosScore} \n" +
            $"| Target Chaos: {targetChaos?.ToString() ?? "N/A"} \n" +
            $"| Raw Weight: {selected.Weight} \n" +
            $"| Chaos Adjusted Weight: {weights[selected]} \n" +
            $"| Weight Sum: {weightSum} \n" +
            $"| Roll Chance: {weights[selected] / weightSum * 100}%",
            _verboseGameDirectorChatLogs
        );

        return selected;
    }

    private int GetTotalPlayerCount(IList<ICommonSession> pool)
    {
        return _antagSelection.GetTotalPlayerCount(pool) + _event.PlayerCountBias;
    }

    private float GetRamping(SecretDoublePlusComponent scheduler)
    {
        return 1f + (float) _ticker.RoundDuration().TotalSeconds * scheduler.SpeedRamping;
    }

    private float GetSavedChaosBudgetMultiplier(SecretDoublePlusComponent scheduler)
    {
        return 1 - _random.NextFloat(Math.Clamp(scheduler.SaveChaosBudgetRange, 0f, 1f));
    }

    private void LogWeightedDict(Dictionary<SelectedEvent, float> weights)
    {
        const string template = "| {0:F2}% | {1,4} | {2,4} | {3:G4} | {4}";
        var weightSum = weights.Values.Sum();
        LogMessage(
            "Chaos Weights: \n" +
            string.Join("\n",
                weights.Select(p =>
                    string.Format(
                        template,
                        p.Value / weightSum * 100,
                        p.Key.ChaosScore,
                        p.Key.Weight,
                        p.Value,
                        p.Key.Proto.ID
                    )
                )
            ),
            false
        );
    }

    private void LogMessage(string message, bool showChat=true)
    {
        // TODO: LogMessage strings all require localization.
        _adminLogger.Add(LogType.SecretDoublePlus, showChat?LogImpact.Medium:LogImpact.High, $"{message}");
        if (showChat)
            _chat.SendAdminAnnouncement("SecretDoublePlus " + message);
    }

    private void ResetMetrics(Entity<SecretDoublePlusComponent> ent, ref ComponentShutdown args)
    {
        SecretDoublePlusMetrics.ChaosScore.Set(0);
    }
}

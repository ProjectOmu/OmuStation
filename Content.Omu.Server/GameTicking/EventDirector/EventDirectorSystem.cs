// SPDX-FileCopyrightText: 2026 Raze500
// SPDX-License-Identifier: AGPL-3.0-or-later

// the event director is omu's scheduler. it runs as its own game rule and owns the full
// round lifecycle cleanly instead of being an entity system on top of another preset.
//
// IMPORTANT: the director is admin-gated per maintainer decision. the "Adventure" preset
// (showInVote: false) is the only preset that boots with OmuEventDirectorRule attached,
// and it is not in the public vote. players never see it. admins reach the director by
// forcing Adventure preset at round start, or running "eventdirector start" mid-round on
// any preset (adds director on top, skips roundstart because the phase is already past).
//
// this system is the "auto-pilot" side of the director. it only reacts to game rule
// lifecycle events (Started, ActiveTick). it does not expose itself to admin commands
// directly - admins talk to EventDirectorAdminSystem, which is the control layer on top.
//
// round structure:
//   roundstart  - fires once when the rule starts, up to 5 reroll attempts
//   loop        - minor fires immediately at roundstart, then repeats:
//                   wait 5-20 min -> roll timer table -> roll minor table -> repeat
//   midround    - fires once independently at a random time between 40-80 minutes
//
// all timings and weights live in eventDirectorConfig + eventDirectorTable yaml prototypes.
// to change round pacing, edit the yaml - no c# changes needed.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Omu.Server.GameTicking.EventDirector;
using Content.Omu.Shared.GameTicking.EventDirector;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Omu.Server.GameTicking.EventDirector;

public sealed class EventDirectorSystem : GameRuleSystem<EventDirectorComponent>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("event-director");

    // maximum roundstart reroll attempts before giving up and starting with no antag
    private const int RoundstartMaxAttempts = 5;

    public override void Initialize()
    {
        base.Initialize();
    }

    // called when the game rule entity becomes active - this is our roundstart
    protected override void Started(EntityUid uid, EventDirectorComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryGetConfig(out var config))
            return;

        _sawmill.Info($"event director started using config '{config.ID}'.");

        RollRoundstartWithRetries(config);

        // first minor fires immediately, no delay
        RollConfiguredTable(config.MinorTable, "minor");

        ScheduleNextLoop(comp, config, TimeSpan.Zero);
        ScheduleFirstMidround(comp, config);
    }

    // called on every tick while the rule is active
    protected override void ActiveTick(EntityUid uid, EventDirectorComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (comp.Paused)
            return;

        if (!TryGetConfig(out var config))
            return;

        var roundTime = GameTicker.RoundDuration();

        if (!comp.MidroundTriggered && comp.NextMidroundRollAt is { } nextMidround && roundTime >= nextMidround)
        {
            RollConfiguredTable(config.MidroundTable, "midround");
            comp.MidroundTriggered = true;
        }

        if (comp.NextLoopFireAt is { } nextLoop && roundTime >= nextLoop)
        {
            RollConfiguredTable(config.TimerTable, "timer");
            RollConfiguredTable(config.MinorTable, "minor");
            ScheduleNextLoop(comp, config, roundTime);
        }
    }

    // --- helpers exposed to EventDirectorAdminSystem ---
    // these are public only so the admin system (same assembly, same folder) can reach them.
    // nothing outside of EventDirectorAdminSystem should be calling these directly.

    public string ActiveConfigId => _cfg.GetCVar(CCVars.EventDirectorConfig);

    // tries to find the currently active director entity and its component
    public bool TryGetActiveDirector([NotNullWhen(true)] out EntityUid? uid, [NotNullWhen(true)] out EventDirectorComponent? comp)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var directorUid, out _, out var directorComp, out _))
        {
            uid = directorUid;
            comp = directorComp;
            return true;
        }

        uid = null;
        comp = null;
        return false;
    }

    public bool SetConfig(string configId, out string message)
    {
        if (!_proto.HasIndex<EventDirectorConfigPrototype>(configId))
        {
            message = $"unknown config '{configId}'. make sure the id matches a eventDirectorConfig prototype.";
            return false;
        }

        _cfg.SetCVar(CCVars.EventDirectorConfig, configId);

        // if a director is running mid-round, reschedule with the new config timings
        if (TryGetActiveDirector(out _, out var comp) &&
            _proto.TryIndex<EventDirectorConfigPrototype>(configId, out var config))
        {
            var roundTime = GameTicker.RoundDuration();
            ScheduleNextLoop(comp, config, roundTime);

            if (!comp.MidroundTriggered)
                ScheduleFirstMidround(comp, config);

            message = $"config switched to '{configId}' mid-round. loop and midround rescheduled.";
            return true;
        }

        message = $"config set to '{configId}'. will take full effect when the next round starts.";
        return true;
    }

    public string BuildStatus()
    {
        if (!TryGetActiveDirector(out _, out var comp))
            return "event director is not active this round.";

        var roundTime = GameTicker.RoundDuration();
        var nextLoop = comp.NextLoopFireAt?.ToString(@"hh\:mm\:ss") ?? "not scheduled";
        var nextMidround = comp.MidroundTriggered ? "already fired" : (comp.NextMidroundRollAt?.ToString(@"hh\:mm\:ss") ?? "not scheduled");

        return $"paused={comp.Paused}, config={ActiveConfigId}, roundTime={roundTime:hh\\:mm\\:ss}, " +
               $"nextLoop={nextLoop}, nextMidround={nextMidround}";
    }

    public IEnumerable<string> DescribeTable(string tableName)
    {
        if (!TryResolveNamedTable(tableName, out var table, out var error))
        {
            yield return error;
            yield break;
        }

        var roundTime = GameTicker.RunLevel == GameRunLevel.InRound ? GameTicker.RoundDuration() : TimeSpan.Zero;
        var readyPlayers = GameTicker.ReadyPlayerCount();

        foreach (var entry in table.Entries)
        {
            var name = entry.Name ?? entry.Rule;
            var reason = TryGetIneligibleReason(entry, readyPlayers, roundTime);
            var status = reason == null ? "eligible" : $"blocked: {reason}";
            yield return $"{entry.Id} -> {name} ({entry.Rule}), weight={entry.Weight}, {status}";
        }
    }

    public bool RollNamedTable(string tableName, out string message)
    {
        if (!TryResolveNamedTable(tableName, out var table, out var error))
        {
            message = error;
            return false;
        }

        return RollTable(table, tableName, out message);
    }

    public bool FireRule(string ruleId, out string message)
    {
        if (!_proto.HasIndex<EntityPrototype>(ruleId))
        {
            message = $"unknown gamerule '{ruleId}'.";
            return false;
        }

        var started = GameTicker.StartGameRule(ruleId);
        message = started
            ? $"started gamerule '{ruleId}'."
            : $"failed to start gamerule '{ruleId}'.";
        return started;
    }

    // --- private scheduling helpers ---

    private void ScheduleNextLoop(EventDirectorComponent comp, EventDirectorConfigPrototype config, TimeSpan fromTime)
    {
        comp.NextLoopFireAt = fromTime + TimeSpan.FromMinutes(_random.Next(config.MinorDelayMinMinutes, config.MinorDelayMaxMinutes + 1));
    }

    private void ScheduleFirstMidround(EventDirectorComponent comp, EventDirectorConfigPrototype config)
    {
        comp.NextMidroundRollAt = TimeSpan.FromMinutes(_random.Next(config.FirstMidroundRollMinMinutes, config.FirstMidroundRollMaxMinutes + 1));
        _sawmill.Info($"midround event scheduled for {comp.NextMidroundRollAt:hh\\:mm\\:ss}.");
    }

    private void RollRoundstartWithRetries(EventDirectorConfigPrototype config)
    {
        if (!_proto.TryIndex(config.RoundStartTable, out var table))
        {
            _sawmill.Error($"roundstart table '{config.RoundStartTable}' was not found.");
            return;
        }

        for (var attempt = 1; attempt <= RoundstartMaxAttempts; attempt++)
        {
            if (RollTable(table, "roundstart", out var message))
            {
                _sawmill.Info(message);
                return;
            }

            _sawmill.Warning($"roundstart attempt {attempt}/{RoundstartMaxAttempts} failed: {message}");
        }

        _sawmill.Warning("roundstart failed all attempts - round begins with no initial antag.");
    }

    private bool RollConfiguredTable(ProtoId<EventDirectorTablePrototype> tableId, string label)
    {
        if (!_proto.TryIndex(tableId, out var table))
        {
            _sawmill.Error($"event director table '{tableId}' was not found.");
            return false;
        }

        if (!RollTable(table, label, out var message))
        {
            _sawmill.Warning(message);
            return false;
        }

        _sawmill.Info(message);
        return true;
    }

    private bool RollTable(EventDirectorTablePrototype table, string label, out string message)
    {
        var roundTime = GameTicker.RunLevel == GameRunLevel.InRound ? GameTicker.RoundDuration() : TimeSpan.Zero;
        var readyPlayers = GameTicker.ReadyPlayerCount();

        var candidates = new List<EventDirectorTableEntry>();
        foreach (var entry in table.Entries)
        {
            if (TryGetIneligibleReason(entry, readyPlayers, roundTime) == null)
                candidates.Add(entry);
        }

        if (candidates.Count == 0)
        {
            message = $"event director could not roll '{label}' - table '{table.ID}' had no eligible entries.";
            return false;
        }

        var totalWeight = candidates.Sum(e => MathF.Max(e.Weight, 0f));
        if (totalWeight <= 0f)
        {
            message = $"event director could not roll '{label}' - table '{table.ID}' has no positive weights.";
            return false;
        }

        var pick = _random.NextFloat(totalWeight);
        var selected = candidates[0];
        foreach (var entry in candidates)
        {
            pick -= MathF.Max(entry.Weight, 0f);
            if (pick > 0f)
                continue;
            selected = entry;
            break;
        }

        var started = GameTicker.StartGameRule(selected.Rule);

        message = started
            ? $"event director rolled '{label}' from table '{table.ID}' -> started '{selected.Rule}' ({selected.Id})."
            : $"event director rolled '{selected.Rule}' from table '{table.ID}', but the gamerule did not start.";
        return started;
    }

    private string? TryGetIneligibleReason(EventDirectorTableEntry entry, int readyPlayers, TimeSpan roundTime)
    {
        if (GameTicker.IsGameRuleActive(entry.Rule))
            return "already active";

        if (readyPlayers < entry.MinimumPlayers)
            return $"needs at least {entry.MinimumPlayers} ready players (have {readyPlayers})";

        if (readyPlayers > entry.MaximumPlayers)
            return $"allows at most {entry.MaximumPlayers} ready players";

        if (roundTime.TotalMinutes < entry.EarliestStartMinutes)
            return $"earliest start is minute {entry.EarliestStartMinutes}";

        if (entry.MaximumOccurrences >= 0 && GetOccurrences(entry.Rule) >= entry.MaximumOccurrences)
            return $"max occurrences ({entry.MaximumOccurrences}) reached";

        if (entry.RepeatDelayMinutes > 0 && GetTimeSinceLastOccurrence(entry.Rule) is { } elapsed
            && elapsed < TimeSpan.FromMinutes(entry.RepeatDelayMinutes))
            return $"repeat delay {entry.RepeatDelayMinutes}m not met";

        return null;
    }

    private int GetOccurrences(string ruleId)
        => GameTicker.AllPreviousGameRules.Count(rule => rule.Item2 == ruleId);

    private TimeSpan? GetTimeSinceLastOccurrence(string ruleId)
    {
        foreach (var (time, rule) in GameTicker.AllPreviousGameRules.Reverse())
        {
            if (rule != ruleId)
                continue;
            return GameTicker.RoundDuration() - time;
        }
        return null;
    }

    private bool TryGetConfig([NotNullWhen(true)] out EventDirectorConfigPrototype? config)
    {
        var id = ActiveConfigId;
        if (_proto.TryIndex(id, out config))
            return true;

        config = null;
        _sawmill.Error($"event director config '{id}' was not found.");
        return false;
    }

    private bool TryResolveNamedTable(string tableName, [NotNullWhen(true)] out EventDirectorTablePrototype? table, out string error)
    {
        error = string.Empty;
        table = null;

        if (!TryGetConfig(out var config))
        {
            error = $"active config '{ActiveConfigId}' was not found.";
            return false;
        }

        ProtoId<EventDirectorTablePrototype> tableId = tableName.ToLowerInvariant() switch
        {
            "roundstart" => config.RoundStartTable,
            "minor"      => config.MinorTable,
            "midround"   => config.MidroundTable,
            "timer"      => config.TimerTable,
            _            => default
        };

        if (tableId == default)
        {
            error = $"unknown table '{tableName}'. use: roundstart, minor, midround, timer.";
            return false;
        }

        if (_proto.TryIndex(tableId, out table))
            return true;

        table = null;
        error = $"table prototype '{tableId}' was not found.";
        return false;
    }
}

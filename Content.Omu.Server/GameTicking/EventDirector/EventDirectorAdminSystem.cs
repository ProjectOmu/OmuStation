// SPDX-FileCopyrightText: 2026 Raze500
// SPDX-License-Identifier: AGPL-3.0-or-later

// this is the admin-facing side of the event director.
//
// the preset (EventDirectorSystem) just runs its roundstart + loop + midround on auto-pilot.
// this admin system is what the /eventdirector command talks to. it does not run on its own -
// it only does work when an admin explicitly asks it to.
//
// gating: the director is admin-only right now (maintainer decision while it is being
// tested live). Adventure preset exists but is not voteable (showInVote: false in
// Resources/Prototypes/_Omu/game_presets.yml). admins are the only way the director runs:
// either by forcing Adventure at round start or by calling Start() here mid-round.
//
// responsibilities:
//   - start/stop the director game rule manually in the current round
//     (so admins can trigger the director in any preset, not just the one that auto-starts it)
//   - inspect and mutate the running director (status, pause, resume, setconfig)
//   - force rolls or specific gamerules outside the normal schedule
//
// keeping the admin logic in its own system follows maintainer feedback: the preset should
// focus on "what happens when nobody is looking" and the admin tool should focus on
// "what happens when a game admin wants to intervene".

using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Omu.Server.GameTicking.EventDirector;

public sealed class EventDirectorAdminSystem : EntitySystem
{
    [Dependency] private readonly EventDirectorSystem _director = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // proto id of the game rule entity that carries the event director component.
    // starting this rule is what flips the director from "idle" to "running".
    private const string DirectorRuleId = "OmuEventDirectorRule";

    public string ActiveConfigId => _cfg.GetCVar(CCVars.EventDirectorConfig);

    // start the director in the current round, regardless of which preset is active.
    // useful when the round boots into Secret/SecretPlus and an admin wants to take over.
    public bool Start(out string message)
    {
        if (_director.TryGetActiveDirector(out _, out _))
        {
            message = "event director is already active this round.";
            return false;
        }

        if (!_ticker.StartGameRule(DirectorRuleId, out _))
        {
            message = $"failed to start gamerule '{DirectorRuleId}'.";
            return false;
        }

        message = $"event director started using config '{ActiveConfigId}'.";
        return true;
    }

    // stop the active director entity without affecting other schedulers in the round.
    public bool Stop(out string message)
    {
        if (!_director.TryGetActiveDirector(out var uid, out _))
        {
            message = "event director is not active this round.";
            return false;
        }

        _ticker.EndGameRule(uid.Value);
        message = "event director stopped.";
        return true;
    }

    public bool Pause(out string message)
    {
        if (!_director.TryGetActiveDirector(out _, out var comp))
        {
            message = "event director is not active this round.";
            return false;
        }

        comp.Paused = true;
        message = "event director paused.";
        return true;
    }

    public bool Resume(out string message)
    {
        if (!_director.TryGetActiveDirector(out _, out var comp))
        {
            message = "event director is not active this round.";
            return false;
        }

        comp.Paused = false;
        message = "event director resumed.";
        return true;
    }

    // delegations to the preset system. the admin system is the single entry point
    // for the command so the command never talks to the preset directly.
    public string BuildStatus() => _director.BuildStatus();
    public bool SetConfig(string configId, out string message) => _director.SetConfig(configId, out message);
    public bool RollNamedTable(string tableName, out string message) => _director.RollNamedTable(tableName, out message);
    public bool FireRule(string ruleId, out string message) => _director.FireRule(ruleId, out message);
    public IEnumerable<string> DescribeTable(string tableName) => _director.DescribeTable(tableName);
}

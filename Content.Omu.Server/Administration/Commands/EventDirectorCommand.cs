// SPDX-FileCopyrightText: 2026 Raze500
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration;
using Content.Omu.Server.GameTicking.EventDirector;
using Content.Omu.Shared.GameTicking.EventDirector;
using Content.Shared.CCVar;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Administration.Commands;

// IConsoleCommand uses IoC injection (which only knows about registered services).
// EntitySystems are not IoC services and live in IEntitySystemManager,
// so we inject that and resolve EventDirectorAdminSystem at call time instead.
//
// the command only talks to the admin system (EventDirectorAdminSystem), never to the preset
// (EventDirectorSystem) directly. that way the "auto-pilot" and the "admin control" are
// clearly separated and the preset stays focused on its lifecycle job.
//
// permissions: AdminFlags.Admin - any game admin can run this (not restricted to debug flag).
//
// subcommand cheat sheet (kept in sync with Help below):
//   start / stop           - turn the director on/off mid-round without ending the round.
//   status                 - show paused state, active config, next loop/midround times.
//   pause / resume         - freeze/unfreeze timers without stopping the director.
//   scheduler              - print which scheduler mode is active (cvar event.scheduler_mode).
//   setscheduler <mode>    - change scheduler mode. takes effect cleanly on the next round.
//   list <table>           - dump all entries of a table (roundstart|minor|midround|timer)
//                            with weights and whether each entry is currently eligible.
//   roll <table>           - roll a specific table once and fire the result.
//   fire <ruleId>          - start a specific gamerule by id, bypassing tables entirely.
//   setconfig <configId>   - swap the active eventDirectorConfig prototype at runtime.
[AdminCommand(AdminFlags.Admin)]
public sealed class EventDirectorCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private EventDirectorAdminSystem Admin => _entitySystems.GetEntitySystem<EventDirectorAdminSystem>();

    public string Command => "eventdirector";
    public string Description => "Inspect and control Omu's event director (admin tool).";
    public string Help => "eventdirector start | stop | status | scheduler | setscheduler <legacy|secretplus|event-director> | roll <roundstart|minor|midround|timer> | fire <ruleId> | setconfig <configId> | pause | resume | list <roundstart|minor|midround|timer>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "start":
                Admin.Start(out var startMessage);
                shell.WriteLine(startMessage);
                return;

            case "stop":
                Admin.Stop(out var stopMessage);
                shell.WriteLine(stopMessage);
                return;

            case "status":
                shell.WriteLine(Admin.BuildStatus());
                return;

            case "scheduler":
                shell.WriteLine($"active scheduler mode: {_cfg.GetCVar(CCVars.EventSchedulerMode)}");
                shell.WriteLine("recommended: change scheduler in lobby / before the next round for a clean handoff.");
                return;

            case "setscheduler":
                if (args.Length < 2)
                {
                    shell.WriteError("Usage: eventdirector setscheduler <legacy|secretplus|event-director>");
                    return;
                }

                var schedulerMode = args[1].ToLowerInvariant();
                if (schedulerMode is not (CCVars.EventSchedulerModes.Legacy or CCVars.EventSchedulerModes.SecretPlus or CCVars.EventSchedulerModes.EventDirector))
                {
                    shell.WriteError($"Unknown scheduler mode '{args[1]}'. Use: legacy, secretplus, event-director.");
                    return;
                }

                _cfg.SetCVar(CCVars.EventSchedulerMode, schedulerMode);
                shell.WriteLine($"Scheduler mode set to '{schedulerMode}'.");
                shell.WriteLine("recommended: restart the round so the next scheduler takes over cleanly from round start.");
                return;

            case "roll":
                if (args.Length < 2)
                {
                    shell.WriteError("Usage: eventdirector roll <roundstart|minor|midround|timer>");
                    return;
                }

                Admin.RollNamedTable(args[1], out var rollMessage);
                shell.WriteLine(rollMessage);
                return;

            case "fire":
                if (args.Length < 2)
                {
                    shell.WriteError("Usage: eventdirector fire <ruleId>");
                    return;
                }

                Admin.FireRule(args[1], out var fireMessage);
                shell.WriteLine(fireMessage);
                return;

            case "setconfig":
                if (args.Length < 2)
                {
                    shell.WriteError("Usage: eventdirector setconfig <configId>");
                    return;
                }

                Admin.SetConfig(args[1], out var configMessage);
                shell.WriteLine(configMessage);
                return;

            case "pause":
                Admin.Pause(out var pauseMessage);
                shell.WriteLine(pauseMessage);
                return;

            case "resume":
                Admin.Resume(out var resumeMessage);
                shell.WriteLine(resumeMessage);
                return;

            case "list":
                if (args.Length < 2)
                {
                    shell.WriteError("Usage: eventdirector list <roundstart|minor|midround|timer>");
                    return;
                }

                foreach (var line in Admin.DescribeTable(args[1]))
                    shell.WriteLine(line);
                return;

            default:
                shell.WriteError($"Unknown subcommand '{args[0]}'.");
                shell.WriteLine(Help);
                return;
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                new[]
                {
                    new CompletionOption("start"),
                    new CompletionOption("stop"),
                    new CompletionOption("status"),
                    new CompletionOption("scheduler"),
                    new CompletionOption("setscheduler"),
                    new CompletionOption("roll"),
                    new CompletionOption("fire"),
                    new CompletionOption("setconfig"),
                    new CompletionOption("pause"),
                    new CompletionOption("resume"),
                    new CompletionOption("list"),
                },
                "<subcommand>");
        }

        // table name completion for roll and list subcommands
        if (args.Length == 2 && (args[0].Equals("roll", StringComparison.OrdinalIgnoreCase) || args[0].Equals("list", StringComparison.OrdinalIgnoreCase)))
        {
            return CompletionResult.FromHintOptions(
                new[]
                {
                    new CompletionOption("roundstart"),
                    new CompletionOption("minor"),
                    new CompletionOption("midround"),
                    new CompletionOption("timer"),
                },
                "<table>");
        }

        if (args.Length == 2 && args[0].Equals("setscheduler", StringComparison.OrdinalIgnoreCase))
        {
            return CompletionResult.FromHintOptions(
                new[]
                {
                    new CompletionOption(CCVars.EventSchedulerModes.Legacy),
                    new CompletionOption(CCVars.EventSchedulerModes.SecretPlus),
                    new CompletionOption(CCVars.EventSchedulerModes.EventDirector),
                },
                "<mode>");
        }

        if (args.Length == 2 && args[0].Equals("setconfig", StringComparison.OrdinalIgnoreCase))
        {
            // suggest all known eventDirectorConfig prototype ids
            var proto = IoCManager.Resolve<IPrototypeManager>();
            var options = proto.EnumeratePrototypes<EventDirectorConfigPrototype>()
                .Select(p => new CompletionOption(p.ID))
                .ToArray();
            return CompletionResult.FromHintOptions(options, "<configId>");
        }

        return CompletionResult.Empty;
    }
}

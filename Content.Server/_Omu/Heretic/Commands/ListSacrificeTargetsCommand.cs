// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Content.Server._Omu.Heretic.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Heretic.Commands;

[AdminCommand(AdminFlags.Logs)]
public sealed class ListSacrificeTargetsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public string Command => "lssacrificetargets";

    public string Description => "Lists the sacrifice targets of a heretic.";

    public string Help => "lssacrificetargets <username>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        ICommonSession? player;
        if (args.Length > 0)
            _players.TryGetSessionByUsername(args[0], out player);
        else
            player = shell.Player;

        if (player == null)
        {
            shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
            return;
        }

        var minds = _entities.System<SharedMindSystem>();
        if (!minds.TryGetMind(player, out var mindId, out var _))
        {
            shell.WriteError(Loc.GetString("shell-target-entity-does-not-have-message", ("missing", "mind")));
            return;
        }

        if (!_entities.System<ListSacrificeTargetsSystem>().IsHeretic(mindId))
        {
            shell.WriteError(Loc.GetString("shell-target-entity-does-not-have-message", ("missing", "heretic component")));
            return;
        }

        shell.WriteLine($"Sacrifice targets for player {player.Name}:");
        var sacrificeTargets = _entities.System<ListSacrificeTargetsSystem>().GetHereticSacrificeTargets(mindId);
        var sacrificeTargetDatas = sacrificeTargets.ToList();
        if (!sacrificeTargetDatas.Any())
        {
            shell.WriteLine("No sacrifice targets found.");
            return;
        }

        for (var i = 0; i < sacrificeTargetDatas.Count(); i++)
        {
            var ent = sacrificeTargetDatas.ElementAt(i).Entity;
            var jobid = sacrificeTargetDatas.ElementAt(i).Job;
            var jobname = _proto.Index(jobid).LocalizedName;
            var targetname = _entities.System<ListSacrificeTargetsSystem>().GetHereticTargetName(ent);
            shell.WriteLine($"- [{i}] {targetname} ({jobname})");
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(),
                Loc.GetString("shell-argument-username-hint"));
        }

        return CompletionResult.Empty;
    }
}

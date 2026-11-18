// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Content.Omu.Server.Crewlet;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Omu.Server.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class CrewletCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly CrewletSystem _crewlet = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override string Command => "crewlet";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryParseTarget(args[0], shell, out var template))
            return;

        var owner = template;
        if (args.Length == 2 && !TryParseTarget(args[1], shell, out owner))
            return;

        var coords = _transform.GetMapCoordinates(template.Value);
        if (!_crewlet.TryCreate(template.Value, owner.Value, coords, out var crewlet))
        {
            shell.WriteError(Loc.GetString("cmd-crewlet-failed", ("target", args[0])));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-crewlet-success", ("crewlet", EntityManager.ToPrettyString(crewlet.Value))));
    }

    private bool TryParseTarget(string str, IConsoleShell shell, [NotNullWhen(true)] out EntityUid? target)
    {
        if (NetEntity.TryParse(str, out var netEntity)
            && EntityManager.TryGetEntity(netEntity, out target)
            && EntityManager.EntityExists(target))
            return true;

        if (_playerManager.TryGetSessionByUsername(str, out var session) && session.AttachedEntity is { } attached)
        {
            target = attached;
            return true;
        }

        shell.WriteError(session == null
            ? Loc.GetString("cmd-crewlet-not-found", ("target", str))
            : Loc.GetString("cmd-crewlet-no-entity", ("target", str)));

        target = null;
        return false;
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(),
                Loc.GetString("cmd-crewlet-arg-template")),
            2 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(),
                Loc.GetString("cmd-crewlet-arg-owner")),
            _ => CompletionResult.Empty,
        };
    }
}

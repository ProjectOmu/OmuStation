using Content.Omu.Server.Administration.Systems;
using Content.Omu.Shared.Administration;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Omu.Server.Administration.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class ShowObjectivesWindowCommand : LocalizedCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override string Command => "showobjectives";

    public override string Description => Loc.GetString("command-view-objectives-window-desc");
    public override string Help => Loc.GetString("command-view-objectives-window-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is null)
        {
            shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        ICommonSession? player;
        if (args.Length > 0)
            _players.TryGetSessionByUsername(args[0], out player);
        else
            player = shell.Player;

        if (player == null)
        {
            shell.WriteError(Loc.GetString("shell-could-not-find-entity"));
            return;
        }

        var minds = _entityManager.System<SharedMindSystem>();
        if (!minds.TryGetMind(player, out var mindUid, out var mind))
        {
            shell.WriteError(Loc.GetString("shell-target-entity-does-not-have-message", ("missing", "mind")));
            return;
        }

        if (mind.CurrentEntity is null)
        {
            shell.WriteError(Loc.GetString("shell-target-entity-does-not-have-message", ("missing", "attached entity")));
            return;
        }

        if (!_entityManager.TryGetNetEntity(mind.CurrentEntity, out var netTarget))
            return;

        var objectivesWindowSystem = _entityManager.System<ShowObjectivesWindowSystem>();

        if (!objectivesWindowSystem.HasObjectivesToShow((mindUid, mind)))
        {
            shell.WriteError(Loc.GetString("command-view-objectives-window-no-objectives", ("player", mind.CharacterName ?? player.Name)));
            return;
        }

        var message = new ViewObjectivesWindowMessage(netTarget.Value,
                player.Name,
                mind.RoleType,
                mind.Subtype,
                objectivesWindowSystem.GetObjectives((mindUid, mind)),
                objectivesWindowSystem.GetTargets((mindUid, mind)));

        _entityManager.EntityNetManager.SendSystemNetworkMessage(message, shell.Player.Channel);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1 ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), Loc.GetString("shell-argument-username-hint")) : CompletionResult.Empty;
    }
}

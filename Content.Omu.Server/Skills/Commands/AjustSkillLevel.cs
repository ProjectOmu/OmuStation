using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using System.Linq;
using Content.Shared.Mind;
using Content.Shared.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Content.Omu.Shared.Skills.Components;
using Content.Omu.Shared.Skills.Prototypes;
using Content.Omu.Shared.Skills.Systems;
using Content.Omu.Server.Skills;
using Content.Shared._Omu.Skills;


namespace Content.Omu.Server.Skills.Commands;

[AdminCommand(AdminFlags.Logs)]
public sealed class AjustSkillLevelCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;


    public string Command => "ajustskill";

    public string Description => "Changes the given skill by a given amount";

    public string Help => "ajustskill <player> <skill> <amount>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        //ensure we can execute it first
        if (args.Length != 3)
        {
            shell.WriteError(Loc.GetString($"shell-wrong-arguments-number"));
            return;
        }
        //Get mind of target
        if (!_players.TryGetSessionByUsername(args[0], out var data))
        {
            shell.WriteError(Loc.GetString("cmd-addobjective-player-not-found"));
            return;
        }
        if (!_mind.TryGetMind(data, out var mindId, out var mind))
        {
            shell.WriteError(Loc.GetString("cmd-addobjective-mind-not-found"));
            return;
        }
        //ensure skill exists
        if (!_prototypes.TryIndex<SkillPrototype>(args[1], out var skill))
        {
            shell.WriteError(Loc.GetString("cmd-skill-not-valid"));
            return;
        }
        bool validPrototype = false;
        foreach (var iterator in _prototypes.EnumeratePrototypes<SkillPrototype>())
        {
            if (iterator.ID == skill.ID)
            {
                validPrototype = true;
            }
        }
        if (!validPrototype)
        {
            shell.WriteError(Loc.GetString("cmd-skill-not-valid"));
            return;
        }
        //ensure given value is a integer
        if (!int.TryParse(args[2], out var prasedSkillAjustment))
        {
            shell.WriteError(Loc.GetString("cmd-not-a-number"));
            return;
        }
        if (_entities.System<SkillSystem>().TryAjustSkill(mindId, skill, prasedSkillAjustment))
        {
            shell.WriteError(Loc.GetString("cmd-failed-to-ajust-skill"));
            return;
        }
        shell.WriteLine("Skill levels have been altered");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {

        return CompletionResult.Empty;
    }
}
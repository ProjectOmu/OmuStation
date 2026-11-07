using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Omu.Server.MetashieldInformationBagles.Commands;

[AdminCommand(AdminFlags.Logs)]
public sealed class PrintBrokenMetashield : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "printbrokenmetashields";

    public string Description => "prints the metashields that are broken, who broke them and when.";

    public string Help => "printbrokenmetashields";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        MetashieldInformationBagleSystem sys = _entities.System<MetashieldInformationBagleSystem>();
        //ensure we can execute it first
        string output = "The broken metashields are:";
        foreach (var iterator in sys.GetBrokenMetashieldList())
        {
            output += '\n';
            output += iterator.Item1.ToString();
            output += " | " + iterator.Item2;
        }
        shell.WriteLine(output);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.Empty;
    }
}

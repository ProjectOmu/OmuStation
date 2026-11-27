using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class GetChaosCommand : ChaosCommand, IConsoleCommand
{
    public string Command => "getchaos";
    public string Description => "Prints the current chaos score to the console";
    public string Help => "getchaos";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryGetComponent(shell, out var component))
            return;

        shell.WriteLine($"Current secret double plus chaos score: {component.ChaosScore}");
    }
}

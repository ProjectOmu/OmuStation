using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class SetChaosCommand : ChaosCommand, IConsoleCommand
{
    public string Command => "setchaos";
    public string Description => "Sets the current chaos score to the given number";
    public string Help => "setchaos <number>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryParseChaos(shell, args, out var chaos))
            return;

        if (!TryGetComponent(shell, out var component))
            return;

        LogMessage(shell.Player, $"set chaos score to {chaos}");
        component.PendingOperations.Enqueue(new ChaosOperation(ChaosOperationType.Set, (float) chaos));
    }
}

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class AddChaosCommand : ChaosCommand, IConsoleCommand
{
    public string Command => "addchaos";
    public string Description => "Adds chaos (positive or negative) to the current chaos score";
    public string Help => "addchaos <number>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryParseChaos(shell, args, out var chaos))
            return;

        if (!TryGetComponent(shell, out var component))
            return;

        LogMessage(shell.Player, $"added {chaos} chaos score");
        component.PendingOperations.Enqueue(new ChaosOperation(ChaosOperationType.Add, (float) chaos));
    }
}

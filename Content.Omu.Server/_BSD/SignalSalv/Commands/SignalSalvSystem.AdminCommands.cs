
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;


using Content.Omu.Server._BSD.SignalSalv.Components;


namespace Content.Omu.Server._BSD.SignalSalv.Commands;

[AdminCommand(AdminFlags.Logs)]
public sealed class ChangeMaterialIncome : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;


    public string Command => "changematerialincome";

    public string Description => "Changes the amount of materials produced per second for a given map.\n <newIncome> in sheets/100 per second";

    public string Help => "changematerialincome <materialType> <newIncome> <mapID>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        BSDSignalSalvMaterialTransitSystem sigSalvmatTransSys = _entities.System<BSDSignalSalvMaterialTransitSystem>();
        //ensure we can execute it first
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString($"shell-wrong-arguments-number"));
            return;
        }
        if (!sigSalvmatTransSys.ValidMaterial(args[0]))
        {
            shell.WriteError(Loc.GetString("Invalid material type"));
            return;
        }
        if (!int.TryParse(args[1], out var prasedProdRate))
        {
            shell.WriteError(Loc.GetString("production rate was not a number"));
            return;
        }
        sigSalvmatTransSys.OverrideProductionRate(args[0], prasedProdRate);
        shell.WriteLine("Production rate has been altered");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {

        return CompletionResult.Empty;
    }
}
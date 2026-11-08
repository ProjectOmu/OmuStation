using Content.Omu.Server._BSD.IngameConsoleSystem.Components;
using Content.Omu.Shared._BSD.IngameConsoleSystem;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem;
using Content.Omu.Server._BSD.SignalSilo.Components;
using Content.Omu.Server._BSD.IngameServerSystem.Components;
using Content.Omu.Server._BSD.SignalSalv.Helpers;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;
using Content.Omu.Server._BSD.SignalSilo.Prototypes;

using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.SignalSilo;

/// <summary>
/// This system exists purly to facilitate intigration and easy of use for the cargo department
/// TODO: get this thing a proper UI -> WAYS easier to the end user -> stal system till UI done
/// </summary>
public sealed partial class LathGradeMaterialFillerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly BSDIngameServerClientLinkSystem _ingameServerClientLinkSys = default!;
    private static CompleteMaterialList _completeMaterialList = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignalSiloLathegradeMaterialContainerFillerComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommand);
    }
    public void IngameConsoleCommand(Entity<SignalSiloLathegradeMaterialContainerFillerComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        if (args.Type == IngameConsoleCommandType.ICC_PRINT && args.Args != null && args.Args.Length > 2 && args.Args[1] == "options")
        {
            IngameConsoleHistoryChangeEvent ev = new(PrintOptions());
            RaiseLocalEvent(args.TerminalUid, ref ev);
        }
        else if (args.Type == IngameConsoleCommandType.ICC_PRINT && args.Args != null && args.Args.Length > 2 && args.Args[1] == "status")
        {
            IngameConsoleHistoryChangeEvent ev = new(PrintStatus());
            RaiseLocalEvent(args.TerminalUid, ref ev);
        }
        else if (args.Type == IngameConsoleCommandType.ICC_START)
        {
            IngameConsoleHistoryChangeEvent ev = new(TryMakeJug());
            RaiseLocalEvent(args.TerminalUid, ref ev);
        }
        else if (args.Type == IngameConsoleCommandType.ICC_SET && args.Args != null && args.Args.Length > 1)
        {
            IngameConsoleHistoryChangeEvent ev = new();
            RaiseLocalEvent(args.TerminalUid, ref ev);
        }
        return;
    }
    public string PrintOptions()
    {
        var listName = "MaterialFillerOptionList";
        if (!_prototypeManager.Resolve(listName, out MaterialFillListPrototype? optionList) || optionList == null)
        {
            Log.Error($"Tried to get MaterialFillerOptionList and failed");
            return "ERROR - No options found please contact your sys admin";
        }
        string returnString = "";
        foreach (var iterator in optionList.Options)
        {
            if (!_prototypeManager.Resolve(iterator, out MaterialFillPrototype? fillPrototype) || fillPrototype == null)
            {
                continue;
            }
            returnString += Loc.GetString("ingame-console-seperator-line");
            returnString += Loc.GetString("material-filler-option-base", ("ID", fillPrototype.ID));
            foreach (var material in fillPrototype.FillGoal)
            {
                returnString += '\n';
                returnString += material.Key + " -> " + material.Value;
            }
        }
        return returnString;
    }
    public string PrintStatus()
    {
        return "ERROR";
    }
    public string TryMakeJug()
    {
        return "ERROR";
    }

}
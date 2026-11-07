using Content.Omu.Server._BSD.IngameConsoleSystem.Components;
using Content.Omu.Shared._BSD.IngameConsoleSystem;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem;
using Content.Omu.Server._BSD.SignalSilo.Components;
using Content.Omu.Server._BSD.IngameServerSystem.Components;
using Content.Omu.Server._BSD.SignalSalv.Helpers;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;

namespace Content.Omu.Server._BSD.SignalSilo;

/// <summary>
/// This system exists purly to facilitate intigration and easy of use for the cargo department
/// </summary>
public sealed partial class SignalSiloSystem : EntitySystem
{
    [Dependency] private readonly BSDIngameServerClientLinkSystem _ingameServerClientLinkSys = default!;
    private static CompleteMaterialList _completeMaterialList = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignalSiloCargoManagmentComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommand);
    }
    public void IngameConsoleCommand(Entity<SignalSiloCargoManagmentComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        if (args.Type == IngameConsoleCommandType.ICC_PRINT && args.Args != null && args.Args.Length > 3 && args.Args[1] == "status")
        {
            IngameConsoleHistoryChangeEvent ev = new(PrintDepartmentMaterialStatus(ent, args.Args[2]));
            RaiseLocalEvent(args.TerminalUid, ref ev);
        }
        if (args.Type == IngameConsoleCommandType.ICC_PRINT_ALL && args.Args != null && args.Args.Length > 2 && args.Args[1] == "status")
        {
            IngameConsoleHistoryChangeEvent ev = new(PrintAllDepartmentMaterialStatus(ent));
            RaiseLocalEvent(args.TerminalUid, ref ev);
        }
    }

    private string PrintAllDepartmentMaterialStatus(EntityUid self)
    {
        string returnString = "";
        if (!_ingameServerClientLinkSys.TryGetNetworkDownwards(self, "SiloNetwork", out var network)) return "ERROR - NO LINKED ENTITIES";
        foreach (var member in network)
        {
            returnString += PrintDepartmentMaterialStatus(member);
        }
        return returnString;
    }
    private string PrintDepartmentMaterialStatus(EntityUid self, string departmentName)
    {
        string returnString = "";
        if (!_ingameServerClientLinkSys.TryGetNetworkDownwards(self, "SiloNetwork", out var network)) return "ERROR - NO LINKED ENTITIES";
        foreach (var member in network)
        {
            if (!TryComp<SignalSiloDepartmentalMaterialManagmentComponent>(member, out var localManagmentComp)) continue;
            if (localManagmentComp.DepartmentName != departmentName) continue;
            returnString += PrintDepartmentMaterialStatus(member);
        }
        return returnString;
    }

    private string PrintDepartmentMaterialStatus(EntityUid ent)
    {
        string returnString = "";
        if (!TryComp<SignalSiloDepartmentalMaterialManagmentComponent>(ent, out var localManagmentComp)) return returnString;
        if (!TryComp<IngameServerComponent>(ent, out var serverComp)) return returnString;
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(ent, out var infraComp)) return returnString;
        returnString += "Printing information about: " + localManagmentComp.DepartmentName + " NID: " + infraComp.NetworkId.ToString() +
                        "\n -------------------------------------------------------------------" +
                        "\nMaterial Name | Amount";
        foreach (var iterator in serverComp.StoredPoints.Keys)
        {
            if (_completeMaterialList.AllMaterialTypes.Contains((string) iterator))
            {
                returnString += "\n" + (string) iterator + " | " + serverComp.StoredPoints[iterator].ToString();
            }
        }
        returnString += "\n -------------------------------------------------------------------";
        return returnString;
    }
}
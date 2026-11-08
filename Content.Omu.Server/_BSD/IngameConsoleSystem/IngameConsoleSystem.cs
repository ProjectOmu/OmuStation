using System.Linq;

using Robust.Server.GameObjects;

using Content.Omu.Shared._BSD.IngameConsoleSystem;
using Content.Omu.Server._BSD.IngameConsoleSystem.Components;

using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem;
using Content.Omu.Server._BSD.MultiBlockSystem.Components;


namespace Content.Omu.Server._BSD.IngameConsoleSystem;

public sealed partial class BSDIngameConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly BSDIngameServerClientLinkSystem _ingameServerClientLink = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IngameConsoleComponent, IngameConsoleHistoryChangeEvent>(IngameConsoleHistoryChangeViaEvent);
        SubscribeLocalEvent<IngameConsoleComponent, IngameConsoleCommandAttemptMessage>(OnCommandAttempt);
        SubscribeLocalEvent<IngameConsoleComponent, IngameConsoleCommandCalledEvent>(OnCommand);

        SubscribeLocalEvent<IngameConsoleActiveProxyComponent, IngameConsoleCommandCalledEvent>(ProxyRelayCommands);
    }
    #region ConsoleHistory handeling
    public void IngameConsoleHistoryChangeViaEvent(Entity<IngameConsoleComponent> ent, ref IngameConsoleHistoryChangeEvent args)
    {
        IngameConsoleHistoryChange(ent, args.AddToHistory);
        return;
    }
    public void IngameConsoleHistoryChange(Entity<IngameConsoleComponent> ent, string historyAddition)
    {
        if (!TryComp<IngameConsoleComponent>(ent, out var comp)) return;
        foreach (string iterator in historyAddition.Split("%n"))
        {
            if (iterator == null) continue;
            comp.History.Add(iterator);
        }
        var state = new IngameConsoleBoundUserInterfaceState(
            comp.History.ToArray<string>());
        _uiSystem.SetUiState(ent.Owner, IngameConsoleUiKey.Key, state);
        return;
    }
    public void IngameConsoleHistoryReset(EntityUid ent)
    {
        if (!TryComp<IngameConsoleComponent>(ent, out var comp)) return;
        comp.History = new List<string>(["Start"]);
        var state = new IngameConsoleBoundUserInterfaceState(
            comp.History.ToArray<string>());
        _uiSystem.SetUiState(ent, IngameConsoleUiKey.Key, state);
        return;
    }
    #endregion
    #region Command handeling
    public void OnCommandAttempt(Entity<IngameConsoleComponent> ent, ref IngameConsoleCommandAttemptMessage args)
    {
        EntityUid targetUid = ent;
        if (TryComp<MultiBlockPartComponent>(ent, out var partComp))
        {
            if (partComp.ConstrollEntity != null)
            {
                targetUid = (EntityUid) partComp.ConstrollEntity;
            }
        }
        string[] splitInput = args.InputString.Split(' ');
        IngameConsoleCommandList ingameCommandList = new();
        if (!TryComp<IngameConsoleComponent>(ent, out var comp)) return;
        IngameConsoleHistoryChangeEvent evHistory = new(args.InputString);
        RaiseLocalEvent(ent, ref evHistory);
        if (TryComp<IngameConsoleActiveProxyComponent>(targetUid, out var compProxy))
        {
            if (!TryComp<IngameServerClientLinkInfrastructureComponent>(targetUid, out var compInfra)) return;
            string appendedString = "<PROXY FROM:" + compInfra.DeviceName + "(" + compInfra.NetworkId + ")send command:%n->";
            appendedString += args.InputString;
            OnProxyCommand(compProxy.ProxyTarget, splitInput, appendedString);
            return;
        }
        foreach (IngameConsoleCommand iterator in ingameCommandList.List)
        {
            if (!iterator.UniversalCommand)
            {
                if (!comp.AllowedTypes.Contains(iterator.Type)) continue;
            }
            if (iterator.Key != splitInput[0]) continue;
            if (iterator.ArgumentsNumberMin > splitInput.Length) continue;//ensure we got enought arguments
            IngameConsoleCommandCalledEvent ev = new(iterator.Type, ent, splitInput);//still ships the type with it, aka start reading AFTER index 0 
            RaiseLocalEvent(targetUid, ref ev);
            return;
        }
        return;
    }
    public void OnCommand(Entity<IngameConsoleComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        if (args.Type == IngameConsoleCommandType.ICC_CLS_EXCLUSIVE)
        {
            IngameConsoleHistoryReset(args.TerminalUid);
        }
        else if (args.Type == IngameConsoleCommandType.ICC_PROXY && args.Args!.Length > 2 && args.Args[1] == "link")
        {
            TryProxy(ent, args.Args);
        }
        else if (args.Type == IngameConsoleCommandType.ISCL_PROXY_TERMINATE && args.Args!.Length > 1)
        {
            if (!Int32.TryParse(args.Args[1], out int netID)) return;
            if (!TryComp<IngameServerClientLinkInfrastructureComponent>(ent, out var compInfra)) return;
            if (netID != compInfra.NetworkId) return;
            OnProxyEnd(ent);
        }
    }
    #endregion
    #region Assistance in converstion
    //returns false by default aka this detects true only
    HashSet<string> _waysToSayTrue = new HashSet<string> { "y", "yes", "true", "Y", "Yes", "TRUE", "True", "YES" };
    public bool InputBoolCheck(string input)
    {
        if (_waysToSayTrue.Contains(input)) return true;
        return false;
    }
    #endregion
}
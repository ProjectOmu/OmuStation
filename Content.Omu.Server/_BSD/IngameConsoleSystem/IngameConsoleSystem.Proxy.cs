using Content.Omu.Shared._BSD.IngameConsoleSystem;

using Content.Omu.Server._BSD.IngameConsoleSystem.Components;

using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;

namespace Content.Omu.Server._BSD.IngameConsoleSystem;

public sealed partial class BSDIngameConsoleSystem : EntitySystem
{
    public bool TryProxy(EntityUid proxyController, string[] input)//TODO: add error messages why it failed
    {
        if (HasComp<IngameConsoleActiveProxyComponent>(proxyController)) return false;
        if (!TryComp<IngameConsoleComponent>(proxyController, out var compConsoleControll)) return false;
        if (!compConsoleControll.PermitsProxy[input[1]]) return false;
        var querry = EntityQueryEnumerator<IngameServerClientLinkInfrastructureComponent>();
        if (Int32.TryParse(input[2], out int netID)) return false;
        if (_ingameServerClientLink.TryGetEntityUidFromNetID(netID, out var proxyTarget)) return false;
        if (!TryComp<IngameConsoleComponent>(proxyTarget, out var compConsoleTarget)) return false;
        if (!compConsoleTarget.PermitsProxy[input[1]]) return false;
        if (!_ingameServerClientLink.TryGetNetworkTotal(proxyController, input[1], out var network)) return false;
        if (!network.Contains((EntityUid) proxyTarget)) return false;
        OnProxyStart(proxyController, (EntityUid) proxyTarget, input[1]);
        return false;
    }
    public void OnProxyStart(EntityUid proxyController, EntityUid proxyTarget, string channel)
    {
        EnsureComp<IngameConsoleActiveProxyComponent>(proxyController, out var compContoll);
        compContoll.Channel = channel;
        compContoll.ProxyTarget = proxyTarget;
        EnsureComp<IngameConsoleActiveProxyTargetComponent>(proxyTarget, out var compTarget);
        compTarget.ProxyContollers.Add(proxyController);
        return;
    }
    public void OnProxyEnd(EntityUid proxyController)
    {
        if (!TryComp<IngameConsoleActiveProxyComponent>(proxyController, out var compProxy)) return;
        if (TryComp<IngameConsoleActiveProxyTargetComponent>(compProxy.ProxyTarget, out var compTarget))
        {
            compTarget.ProxyContollers.Remove(proxyController);
        }
        RemComp<IngameConsoleActiveProxyComponent>(proxyController);
        return;
    }
    public void OnProxyCommand(EntityUid ent, string[] splitInput, string appendedInput, HashSet<EntityUid>? pastProxies = null)
    {
        IngameConsoleCommandList ingameCommandList = new();
        if (!TryComp<IngameConsoleComponent>(ent, out var comp)) return;
        IngameConsoleHistoryChangeEvent evHistory = new(appendedInput);
        RaiseLocalEvent(ent, ref evHistory);
        if (TryComp<IngameConsoleActiveProxyComponent>(ent, out var compProxy))
        {
            if (pastProxies != null && pastProxies.Contains(ent)) return;//prevents admins or players from causing a infinte loop
            if (pastProxies == null) pastProxies = new();
            pastProxies.Add(ent);
            if (!TryComp<IngameServerClientLinkInfrastructureComponent>(ent, out var compInfra)) return;
            string appendedString = "<PROXY FROM:" + compInfra.DeviceName + "(" + compInfra.NetworkId + ")send command:%n->";
            appendedString += appendedInput;
            OnProxyCommand(compProxy.ProxyTarget, splitInput, appendedString, pastProxies);
            return;
        }
        foreach (IngameConsoleCommand iterator in ingameCommandList.List)
        {
            if (!comp.AllowedTypes.Contains(iterator.Type)) continue;
            if (iterator.Key != splitInput[0]) continue;
            if (iterator.ArgumentsNumberMin > splitInput.Length) continue;//ensure we got enought arguments
            IngameConsoleCommandCalledEvent ev = new(iterator.Type, ent, splitInput);//still ships the type with it, aka start reading AFTER index 0 
            RaiseLocalEvent(ent, ref ev);
            return;
        }
    }

    public void ProxyRelayCommands(Entity<IngameConsoleActiveProxyComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        //ensure proper handeling to allow selective endings of proxies rn relays all the way down and termiantes them all
        if (args.Args != null)
        {
            foreach (var iterator in args.Args)
            {
                if (iterator == "-l")//local command key does not get relayed
                {
                    return;
                }
            }
        }
        if (!_ingameServerClientLink.CheckTransmissionRange(ent, ent.Comp.ProxyTarget, ent.Comp.Channel)) return;
        var ev = args;
        RaiseLocalEvent(ent.Comp.ProxyTarget, ref ev);
    }
    public void ProxyRelayHistory(Entity<IngameConsoleActiveProxyTargetComponent> ent, ref IngameConsoleHistoryChangeEvent args)
    {
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(ent, out var compInfra)) return;
        string relayMessage = Loc.GetString("ICS_Proxy_Relay_Info", ("NID", compInfra.NetworkId));
        relayMessage += args.AddToHistory;
        var ev = new IngameConsoleHistoryChangeEvent(relayMessage);
        foreach (var iterator in ent.Comp.ProxyContollers)
        {
            RaiseLocalEvent(iterator, ref ev);
        }
    }
}
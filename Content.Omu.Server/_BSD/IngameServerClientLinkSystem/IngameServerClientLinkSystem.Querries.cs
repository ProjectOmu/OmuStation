
using System.Numerics;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;

namespace Content.Omu.Server._BSD.IngameServerClientLinkSystem;

public sealed partial class BSDIngameServerClientLinkSystem : EntitySystem
{
    public bool TryGetNetworkTotal(EntityUid self, string connectionType, out HashSet<EntityUid> entityHash)
    {
        entityHash = new();
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(self, out var infraComp)) return false;
        return TryGetNetworkTotal(self, connectionType, infraComp, out entityHash);
    }
    public bool TryGetNetworkTotal(EntityUid self, string connectionType, IngameServerClientLinkInfrastructureComponent infraComp, out HashSet<EntityUid> entityHash)
    {
        entityHash = new();
        HashSet<EntityUid> toSearchSet = new();
        HashSet<EntityUid> nextToSearchSet = new();
        toSearchSet.UnionWith(infraComp.EntityDicClient[connectionType]);
        toSearchSet.UnionWith(infraComp.EntityDicServer[connectionType]);
        entityHash.UnionWith(infraComp.EntityDicClient[connectionType]);
        entityHash.UnionWith(infraComp.EntityDicServer[connectionType]);
        entityHash.Add(self);
        while (toSearchSet.Count > 0)
        {
            nextToSearchSet.Clear();
            foreach (var iterator in toSearchSet)
            {
                if (!TryComp<IngameServerClientLinkInfrastructureComponent>(iterator, out var iteratorComp))
                {
                    //TODO: Clense the connection as the entity does not have the infra comp
                    continue;
                }
                nextToSearchSet.UnionWith(iteratorComp.EntityDicClient[connectionType]);
                nextToSearchSet.UnionWith(iteratorComp.EntityDicServer[connectionType]);
            }
            entityHash.UnionWith(toSearchSet);
            toSearchSet.Clear();
            toSearchSet.UnionWith(nextToSearchSet);
            toSearchSet.ExceptWith(entityHash);
        }
        return true;
    }
    public bool TryGetNetworkDownwards(EntityUid self, string connectionType, out HashSet<EntityUid> entityHash)
    {
        entityHash = new();
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(self, out var infraComp)) return false;
        return TryGetNetworkDownwards(self, connectionType, infraComp, out entityHash);
    }
    public bool TryGetNetworkDownwards(EntityUid self, string connectionType, IngameServerClientLinkInfrastructureComponent infraComp, out HashSet<EntityUid> entityHash)
    {
        entityHash = new();
        HashSet<EntityUid> toSearchSet = new();
        HashSet<EntityUid> nextToSearchSet = new();
        toSearchSet.UnionWith(infraComp.EntityDicServer[connectionType]);
        entityHash.UnionWith(infraComp.EntityDicServer[connectionType]);
        entityHash.Add(self);
        while (toSearchSet.Count > 0)
        {
            nextToSearchSet.Clear();
            foreach (var iterator in toSearchSet)
            {
                if (!TryComp<IngameServerClientLinkInfrastructureComponent>(iterator, out var iteratorComp))
                {
                    //TODO: Clense the connection as the entity does not have the infra comp
                    continue;
                }
                nextToSearchSet.UnionWith(iteratorComp.EntityDicServer[connectionType]);
            }
            entityHash.UnionWith(toSearchSet);
            toSearchSet.Clear();
            toSearchSet.UnionWith(nextToSearchSet);
            toSearchSet.ExceptWith(entityHash);
        }
        return true;
    }
    public bool TryGetNetworkUpwards(EntityUid self, string connectionType, out HashSet<EntityUid> entityHash)
    {
        entityHash = new();
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(self, out var infraComp)) return false;
        return TryGetNetworkUpwards(self, connectionType, infraComp, out entityHash);
    }
    public bool TryGetNetworkUpwards(EntityUid self, string connectionType, IngameServerClientLinkInfrastructureComponent infraComp, out HashSet<EntityUid> entityHash)
    {
        entityHash = new();
        HashSet<EntityUid> toSearchSet = new();
        HashSet<EntityUid> nextToSearchSet = new();
        toSearchSet.UnionWith(infraComp.EntityDicClient[connectionType]);
        entityHash.UnionWith(infraComp.EntityDicClient[connectionType]);
        entityHash.Add(self);
        while (toSearchSet.Count > 0)
        {
            nextToSearchSet.Clear();
            foreach (var iterator in toSearchSet)
            {
                if (!TryComp<IngameServerClientLinkInfrastructureComponent>(iterator, out var iteratorComp))
                {
                    //TODO: Clense the connection as the entity does not have the infra comp
                    continue;
                }
                nextToSearchSet.UnionWith(iteratorComp.EntityDicClient[connectionType]);
            }
            entityHash.UnionWith(toSearchSet);
            toSearchSet.Clear();
            toSearchSet.UnionWith(nextToSearchSet);
            toSearchSet.ExceptWith(entityHash);
        }
        return true;
    }

    public bool TryGetEntityUidFromNetID(int netID, out EntityUid? entUid)
    {
        entUid = null;
        var querry = EntityQueryEnumerator<IngameServerClientLinkInfrastructureComponent>();
        while (querry.MoveNext(out var interatorEnt, out var iteratorComp))
        {
            if (iteratorComp.NetworkId == netID)
            {
                entUid = interatorEnt;
                return true;
            }
        }
        return false;
    }
    public List<int> GetAllAvailableConnectionsNetID(EntityUid originEnt, string channel)
    {
        List<int> returnList = new();
        var querry = EntityQueryEnumerator<IngameServerClientLinkInfrastructureComponent>();
        while (querry.MoveNext(out var interatorEnt, out var iteratorComp))
        {
            if (CheckTransmissionRange(originEnt, interatorEnt, channel))
            {
                returnList.Add(iteratorComp.NetworkId);
            }
        }
        return returnList;
    }
    public bool CheckTransmissionRange(EntityUid server, EntityUid client, string channel, bool anyOverride = false)
    {
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(server, out var compInfaOne)) return false;
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(client, out var compInfaTwo)) return false;
        EnsureValidChannelExistance(compInfaOne, channel);
        EnsureValidChannelExistance(compInfaTwo, channel);
        var compTransOne = Transform(server);
        var compTransTwo = Transform(client);
        byte accessBase = 0;
        if (compInfaOne.GlobalyAccessable[channel] || (compInfaTwo.GlobalyAccessable[channel] && anyOverride))
        {
            accessBase = 3;
        }
        else if (compInfaOne.MapWideAccessable[channel] || (compInfaTwo.MapWideAccessable[channel] && anyOverride))
        {
            accessBase = 2;
        }
        else if (compInfaOne.GridWideAccessable[channel] || (compInfaTwo.GridWideAccessable[channel] && anyOverride))
        {
            accessBase = 1;
        }
        switch (accessBase)
        {
            case 3:
                return true;
            case 2:
                if (compTransOne.MapID == compTransTwo.MapID) return true;
                return false;
            case 1:
                if (compTransOne.MapID != compTransTwo.MapID) return false;
                if (compTransOne.GridUid == compTransTwo.GridUid) return true;
                break;
        }
        if (compTransOne.MapID != compTransTwo.MapID) return false;
        //if we reach here we are on same map but not grid, but we may still be in range of the emmissions
        float connectionDistance = compInfaOne.ConnectionRadius[channel];
        if (anyOverride)
        {
            connectionDistance = Math.Max(compInfaOne.ConnectionRadius[channel], compInfaTwo.ConnectionRadius[channel]);
        }
        if (connectionDistance < Vector2.Distance(compTransOne.Coordinates.Position, compTransTwo.Coordinates.Position))
            return false;
        return true;
    }
    /// <summary>
    /// This function ensure that every dictonary contains a defention for the given channel,
    /// thereby setting the default values so to speak
    /// </summary>
    /// <param name="comp"></param>
    /// <param name="channel"></param>
    private void EnsureValidChannelExistance(IngameServerClientLinkInfrastructureComponent comp, string channel)
    {
        if (!comp.ConnectionRadius.ContainsKey(channel)) comp.ConnectionRadius.Add(channel, 0);
        if (!comp.ServerPriority.ContainsKey(channel)) comp.ServerPriority.Add(channel, 0);
        if (!comp.MaxAmountConnectionClientServer.ContainsKey(channel)) comp.MaxAmountConnectionClientServer.Add(channel, int.MaxValue);
        if (!comp.MaxAmountConnectionServerClient.ContainsKey(channel)) comp.MaxAmountConnectionServerClient.Add(channel, int.MaxValue);
        if (!comp.GridWideAccessable.ContainsKey(channel)) comp.GridWideAccessable.Add(channel, false);
        if (!comp.MapWideAccessable.ContainsKey(channel)) comp.MapWideAccessable.Add(channel, false);
        if (!comp.GlobalyAccessable.ContainsKey(channel)) comp.GlobalyAccessable.Add(channel, false);
        if (!comp.ServerNeedsToIniciate.ContainsKey(channel)) comp.ServerNeedsToIniciate.Add(channel, false);
        if (!comp.EntityDicClient.ContainsKey(channel)) comp.EntityDicClient.Add(channel, new());
        if (!comp.EntityDicServer.ContainsKey(channel)) comp.EntityDicServer.Add(channel, new());
    }
}
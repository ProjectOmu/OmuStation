using System.Linq;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;
using YamlDotNet.Core.Tokens;

namespace Content.Omu.Server._BSD.IngameServerClientLinkSystem;

public sealed partial class BSDIngameServerClientLinkSystem : EntitySystem
{
    /// <summary>
    /// Tries to establish a link between 2 locations
    /// Server range is proritised
    /// if both sides have same priority then the start of the connection will be the client
    /// </summary>
    /// <param name="startOfConnection"></param>
    /// <param name="targetOfConnection"></param>
    /// <param name="channel"></param>
    /// <param name="connectAsClient">override always be client</param>
    /// <param name="connectAsServer">override always be server</param>
    /// <returns></returns>
    public bool TryEstablishLink(EntityUid startOfConnection, EntityUid targetOfConnection, string channel, bool connectAsClient = false, bool connectAsServer = false)
    {
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(startOfConnection, out var compStart)) return false;
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(targetOfConnection, out var compTarget)) return false;
        EnsureValidChannelExistance(compStart, channel);
        EnsureValidChannelExistance(compTarget, channel);
        if (connectAsClient || compStart.ServerPriority[channel] <= compTarget.ServerPriority[channel])
        {
            return TryConnectAsClient(startOfConnection, targetOfConnection, channel, compStart, compTarget);
        }
        if (connectAsServer || compStart.ServerPriority[channel] > compTarget.ServerPriority[channel])
        {
            return TryConnectAsServer(startOfConnection, targetOfConnection, channel, compStart, compTarget);
        }
        Log.Error("IngameServerClientLinkSystem.Linking[TryEstablishLink()] --> somehow no connection was established and no exit happened take a look");
        return false;//something went seriously wrong
    }
    public void TerminateLink(EntityUid startOfConnection, EntityUid targetOfConnection, string channel)
    {
        bool startPresent = true;
        bool targetPresent = true;
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(startOfConnection, out var compStart)) startPresent = false;
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(targetOfConnection, out var compTarget)) targetPresent = false;
        if (startPresent)
        {
            compStart!.EntityDicClient[channel].Remove(targetOfConnection);
            compStart!.EntityDicServer[channel].Remove(targetOfConnection);
        }
        if (targetPresent)
        {
            compTarget!.EntityDicClient[channel].Remove(startOfConnection);
            compTarget!.EntityDicServer[channel].Remove(startOfConnection);
        }
        return;
    }
    private bool TryConnectAsClient(EntityUid client, EntityUid server, string channel, IngameServerClientLinkInfrastructureComponent clientComp, IngameServerClientLinkInfrastructureComponent serverComp)
    {
        if (serverComp.ServerNeedsToIniciate[channel]) return false;
        if (!CheckTransmissionRange(server, client, channel)) return false;
        if (serverComp.MaxAmountConnectionServerClient[channel] < 1 + serverComp.EntityDicClient.Count) return false;
        if (clientComp.MaxAmountConnectionClientServer[channel] < 1 + clientComp.EntityDicServer.Count) return false;
        serverComp.EntityDicClient[channel].Add(client);
        clientComp.EntityDicServer[channel].Add(server);
        return true;
    }
    private bool TryConnectAsServer(EntityUid server, EntityUid client, string channel, IngameServerClientLinkInfrastructureComponent serverComp, IngameServerClientLinkInfrastructureComponent clientComp)
    {
        if (!CheckTransmissionRange(server, client, channel)) return false;
        if (serverComp.MaxAmountConnectionServerClient[channel] < 1 + serverComp.EntityDicClient.Count) return false;
        if (clientComp.MaxAmountConnectionClientServer[channel] < 1 + clientComp.EntityDicServer.Count) return false;
        serverComp.EntityDicClient[channel].Add(client);
        clientComp.EntityDicServer[channel].Add(server);
        return true;
    }
}
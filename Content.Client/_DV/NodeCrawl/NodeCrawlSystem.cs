using Content.Client.SubFloor;
using Content.Shared._DV.NodeCrawl;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._DV.NodeCrawl;

public sealed class NodeCrawlSystem : SharedNodeCrawlSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SubFloorHideSystem _subfloor = default!;
    [Dependency] private readonly IGameTiming _timing = default!;   //Omu
    [Dependency] private readonly IComponentFactory _componentFactory = default!;  //Omu

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NodeCrawlerComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<NodeCrawlerComponent, LocalPlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<NodeCrawlerComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAttached(Entity<NodeCrawlerComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        if (ent.Comp.Mover is not null)
            _subfloor.Types = ent.Comp.RevealedComponents;
    }

    private void OnDetached(Entity<NodeCrawlerComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _subfloor.Types = new Type[] { };
    }

    private void OnAfterAutoHandleState(Entity<NodeCrawlerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity != ent)
            return;

        ent.Comp.RevealedComponents = GetRevealedComponents(ent);

        if (ent.Comp.Mover is not null)
            _subfloor.Types = ent.Comp.RevealedComponents;
        else
            _subfloor.Types = new Type[] { };
    }

    private Type[] GetRevealedComponents(NodeCrawlerComponent component)
    {
        if (component.NetworkedComponents is null)
            return Array.Empty<Type>();

        var types = new List<Type>();

        foreach (var name in component.NetworkedComponents)
        {
            if (_componentFactory.TryGetRegistration(name, out var registration))
                types.Add(registration.Type);
        }

        return types.ToArray();
    }
}

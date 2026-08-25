using Content.Shared.Follower;

namespace Content.Omu.Server.Entities.Heretic;

public sealed class LodestoneSystem : EntitySystem
{
    [Dependency] private readonly FollowerSystem _follow = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IEntityManager _entManager = null!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LodestoneComponent, EntityStartedFollowingEvent>(OnStartFollowing);
        SubscribeLocalEvent<LodestoneComponent, EntityStoppedFollowingEvent>(OnStopFollowing);
    }

    public void OnStartFollowing(Entity<LodestoneComponent> ent, ref EntityStartedFollowingEvent args)
    {
        if (ent.Comp.AddedComponents is null)
            return;

        foreach (var comp in ent.Comp.AddedComponents)
        {
            if (!_componentFactory.TryGetRegistration(comp.Key, out var registration))
                continue;

            if (_entManager.HasComponent(args.Following, registration))
                continue;

            var comptoadd = _componentFactory.GetComponent(registration);

            _entManager.AddComponent(args.Following, comptoadd);

            ent.Comp.ComponentsActuallyAdded.Add(comp.Key);
        }
    }

    public void OnStopFollowing(Entity<LodestoneComponent> ent, ref EntityStoppedFollowingEvent args)
    {
        if (ent.Comp.ComponentsActuallyAdded is null)
            return;

        foreach (var comp in ent.Comp.ComponentsActuallyAdded)
        {
            if (!_componentFactory.TryGetRegistration(comp, out var comprem))
                continue;

            var comptorem = _componentFactory.GetComponent(comprem);
            _entManager.RemoveComponent(args.Following, comptorem);
        }
        ent.Comp.ComponentsActuallyAdded.Clear();
    }
}

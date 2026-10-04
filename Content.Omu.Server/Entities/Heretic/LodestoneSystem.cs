using Content.Shared.Follower;
using Content.Shared.Popups;

namespace Content.Omu.Server.Entities.Heretic;

public sealed class LodestoneSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IEntityManager _entManager = null!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LodestoneComponent, StartedFollowingEntityEvent>(OnStartFollowing);
        SubscribeLocalEvent<LodestoneComponent, StoppedFollowingEntityEvent>(OnStopFollowing);
    }

    public void OnStartFollowing(Entity<LodestoneComponent> ent, ref StartedFollowingEntityEvent args)
    {
        if (ent.Comp.AddedComponents is null)
            return;

        foreach (var comp in ent.Comp.AddedComponents)
        {
            if (!_componentFactory.TryGetRegistration(comp.Key, out var registration))
                continue;

            if (_entManager.HasComponent(args.Following, registration))
                continue;

            var comptoadd = _componentFactory.GetComponent(comp.Value);

            _entManager.AddComponent(args.Following, comptoadd);

            ent.Comp.ComponentsActuallyAdded.Add(comp.Key);
        }

        if (ent.Comp.ComponentsActuallyAdded.Count > 0)
            _popup.PopupEntity(Loc.GetString("lodestone-power"), args.Following, args.Following, PopupType.Medium);
    }

    public void OnStopFollowing(Entity<LodestoneComponent> ent, ref StoppedFollowingEntityEvent args)
    {
        List<string> badComps = new();
        foreach (var comp in ent.Comp.ComponentsActuallyAdded)
        {
            if (!_componentFactory.TryGetRegistration(comp, out var registration) || !_entManager.HasComponent(args.Following, registration.Type))
            {
                // Tag for clean up
                badComps.Add(comp);
                continue;
            }

            _entManager.RemoveComponent(args.Following, registration.Type);
        }

        // Clean up removed/bad comps
        foreach (var bad in badComps)
            ent.Comp.ComponentsActuallyAdded.Remove(bad);
    }
}

using Content.Shared._DV.Store.Events;
using Content.Shared.Store.Components;
using Content.Server.Store.Systems;

namespace Content.Server._DV.Store.Systems;

public sealed partial class DVStoreSystem : EntitySystem
{
    [Dependency] private readonly StoreSystem _store = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StoreComponent, IntrinsicStoreActionEvent>(OnIntrinsicStoreAction);
    }

    private void OnIntrinsicStoreAction(Entity<StoreComponent> ent, ref IntrinsicStoreActionEvent args)
    {
        _store.ToggleUi(args.Performer, ent.Owner, ent.Comp);
    }
}

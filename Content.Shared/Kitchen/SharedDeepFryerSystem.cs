using Content.Shared.DragDrop;
using Robust.Shared.Physics.Components;
using Content.Shared.Item;
using Content.Shared.Body.Components;
using Content.Shared.Kitchen.Components;

namespace Content.Shared.Kitchen;

public abstract class SharedDeepFryerSystem : EntitySystem
{
    protected void OnCanDragDropOn(EntityUid uid, SharedDeepFryerComponent component, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = CanInsert(uid, args.Dragged);
        args.Handled = true;
    }

    public virtual bool CanInsert(EntityUid uid, EntityUid entity)
    {
        if (!Transform(uid).Anchored
            || !TryComp(entity, out PhysicsComponent? physics))
            return false;

        var storable = HasComp<ItemComponent>(entity);
        if (!storable && !HasComp<BodyComponent>(entity))
            return false;

        return physics.CanCollide || storable;
    }
}

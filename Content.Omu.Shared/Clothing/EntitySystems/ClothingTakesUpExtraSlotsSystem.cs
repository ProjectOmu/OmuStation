using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Omu.Shared.Clothing.Components;

namespace Content.Omu.Shared.Clothing.EntitySystems;

/// <summary>
///     info.
/// </summary>
public sealed class ClothingTakesUpExtraSlotsSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItemSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingTakesUpExtraSlotsComponent, BeingEquippedAttemptEvent>(OnBeingEquippedAttempt);
        SubscribeLocalEvent<ClothingTakesUpExtraSlotsComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ClothingTakesUpExtraSlotsComponent, GotUnequippedEvent>(OnUnequipped);
    }

    /// <summary>
    ///     Blocks the user from putting on a garment, if they are wearing anything in the slots specified by the component.
    /// </summary>
    private void OnBeingEquippedAttempt(Entity<ClothingTakesUpExtraSlotsComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        foreach (var slot in ent.Comp.Slots)
        {
            // If the inventory slot was found, and it has something in it
            if (_inventorySystem.TryGetSlotContainer(args.EquipTarget, slot, out var slotContainer, out var _)
                && slotContainer.ContainedEntity != null)
            {
                args.Reason = Loc.GetString("slot-block-component-blocked", ("item", slotContainer.ContainedEntity));
                args.Cancel();
                return;
            }
        }
    }

    private void OnEquipped(Entity<ClothingTakesUpExtraSlotsComponent> ent, ref GotEquippedEvent args)
    {
        // spawn a virtual item in each of the slots blocked by this garment.
        foreach (var slot in ent.Comp.Slots)
        {
            // If the slot is taken up, drop the item which is currently in that slot.
            // This check exists to fix a modsuit bug, where you could enable the modsuit, equip an item in a slot which should otherwise be blocked, and then turn off the modsuit, resulting in you wearing an item in a slot which should be blocked.
            if (_inventorySystem.TryGetSlotEntity(args.Equipee, slot, out var _))
            {
                _inventorySystem.DropSlotContents(args.Equipee, slot);
            }

            // if the slot is clear, spawn a virtual item.
            _virtualItemSystem.TrySpawnVirtualItemInInventory(ent, args.Equipee, slot, true);
        }
    }

    private void OnUnequipped(Entity<ClothingTakesUpExtraSlotsComponent> ent, ref GotUnequippedEvent args)
    {
        // remove the virtual items in the slots associated with this garment.
        foreach (var slot in ent.Comp.Slots)
        {
            _virtualItemSystem.DeleteInSlotMatching(args.Equipee, ent);
        }
    }

}
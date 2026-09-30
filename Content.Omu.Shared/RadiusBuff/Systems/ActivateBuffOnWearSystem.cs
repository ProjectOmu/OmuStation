using Content.Omu.Shared.RadiusBuff.Components;
using Content.Shared.Inventory.Events;

namespace Content.Omu.Shared.RadiusBuff.Systems;

public sealed class ActivateBuffOnWearSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivateBuffOnWearComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<ActivateBuffOnWearComponent, GotUnequippedEvent>(OnUnequip);
    }

    private void OnEquip(EntityUid ent, ActivateBuffOnWearComponent comp, GotEquippedEvent args)
    {
        if (!TryComp<RadiusBuffComponent>(ent, out var buffComp))
            return;

        // True if inverted, false if not
        buffComp.Active = !comp.Invert;
        Dirty(ent, comp);
    }

    private void OnUnequip(EntityUid ent, ActivateBuffOnWearComponent comp, GotUnequippedEvent args)
    {
        if (!TryComp<RadiusBuffComponent>(ent, out var buffComp))
            return;

        // False if inverted, true if not
        buffComp.Active = comp.Invert;
        Dirty(ent, comp);
    }
}

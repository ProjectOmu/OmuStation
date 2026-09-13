using Content.Omu.Shared.RadiusBuff.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Omu.Shared.RadiusBuff.Systems;

public sealed class ActivateBuffOnWieldSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivateBuffOnWieldComponent, ItemWieldedEvent>(OnWield);
        SubscribeLocalEvent<ActivateBuffOnWieldComponent, ItemUnwieldedEvent>(OnUnwield);
    }

    private void OnWield(EntityUid ent, ActivateBuffOnWieldComponent comp, ItemWieldedEvent args)
    {
        if (!TryComp<RadiusBuffComponent>(ent, out var buffComp))
            return;

        // Just in case...
        if (!TryComp<WieldableComponent>(ent, out var wield) || !wield.Wielded)
            return;

        // True if inverted, false if not
        buffComp.Active = !comp.Invert;
        Dirty(ent, comp);
    }

    private void OnUnwield(EntityUid ent, ActivateBuffOnWieldComponent comp, ItemUnwieldedEvent args)
    {
        if (!TryComp<RadiusBuffComponent>(ent, out var buffComp))
            return;

        if (!TryComp<WieldableComponent>(ent, out var wield) || wield.Wielded)
            return;

        // False if inverted, true if not
        buffComp.Active = comp.Invert;
        Dirty(ent, comp);
    }
}

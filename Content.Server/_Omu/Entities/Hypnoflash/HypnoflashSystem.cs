using Content.Server._Goobstation.Mindcontrol;
using Content.Goobstation.Shared.Mindcontrol;
using Content.Shared.Flash;
using Content.Shared.Weapons.Melee.Events;
using Content.Server._Omu.Entities.Hypnoflash;
using Content.Shared.Flash.Components;
using System.Linq;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Mind;
using Content.Shared.Drunk;
using Content.Shared.Drugs;
using Content.Shared.Drowsiness;
using Content.Shared.StatusEffectNew;

namespace Content.Goobstation.Server.Implants.Systems;
public sealed class MindcontrolImplantSystem : EntitySystem
{
    [Dependency] private readonly MindcontrolSystem _mindcontrol = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HypnoflashComponent, MeleeHitEvent>(OnFlash);
    }
    private void OnFlash(EntityUid uid, HypnoflashComponent component, MeleeHitEvent args)
    {
        if (!TryComp<FlashComponent>(component.Owner, out var flashcomp))           //Needs a flash... Duh
            return;

        if (!flashcomp.FlashOnMelee ||                                              //Check if it melee'd something
            !args.IsHit ||
            !args.HitEntities.Any())
        {
            return;
        }
        component.FlashUid = component.Owner;
        if (component.FlashUid != null)
        {
            component.HolderUid = Transform(component.FlashUid.Value).ParentUid;
        }
        if (args.HitEntities != null)           //Did we hit smth?
            foreach (var target in args.HitEntities)
            {
                if (_statusEffect.HasStatusEffect(target, "StatusEffectSeeingRainbow") || _statusEffect.HasStatusEffect(target, "StatusEffectDrowsiness") || _statusEffect.HasStatusEffect(target, "StatusEffectForcedSleeping"))
                {
                    EnsureComp<MindcontrolledComponent>(target, out var flashed);        //Mind control em
                    flashed.Master = component.HolderUid;
                    _mindcontrol.Start(target, flashed);
                    continue;
                }
                if (TryComp<DrunkComponent>(target, out _))      //are they susceptible?
                {
                    EnsureComp<MindcontrolledComponent>(target, out var flashed);        //Mind control em
                    flashed.Master = component.HolderUid;
                    _mindcontrol.Start(target, flashed);
                }
            }
    }
}

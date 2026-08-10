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
using Content.Goobstation.Common.Flash;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Entities.Hypnoflash;
public sealed class MindcontrolImplantSystem : EntitySystem
{
    [Dependency] private readonly MindcontrolSystem _mindcontrol = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly SharedChargesSystem _sharedCharges = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    public static readonly ProtoId<TagPrototype> IgnoreResistancesTag = "FlashIgnoreResistances";
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HypnoflashComponent, MeleeHitEvent>(OnFlash);
    }
    private void OnFlash(EntityUid uid, HypnoflashComponent component, MeleeHitEvent args)
    {
        if (!TryComp<FlashComponent>(component.Owner, out var flashcomp))           //Needs a flash... Duh
            return;

        if (TryComp<LimitedChargesComponent>(component.Owner, out var charges)
            && _sharedCharges.IsEmpty((component.Owner, charges)))
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
                var vulnerableEv = new CheckFlashVulnerable();
                RaiseLocalEvent(target, ref vulnerableEv);

                if (component.HolderUid == null
                || !_tag.HasTag(component.Owner, IgnoreResistancesTag)
                && !vulnerableEv.Vulnerable)
                {
                    var attempt = new FlashAttemptEvent(target, component.HolderUid, component.Owner);
                    RaiseLocalEvent(target, ref attempt, true);

                    if (attempt.Cancelled)
                        return;
                }
                if (HasComp<DrunkComponent>(target) || _statusEffect.HasStatusEffect(target, "StatusEffectSeeingRainbow") || _statusEffect.HasStatusEffect(target, "StatusEffectDrowsiness") || _statusEffect.HasStatusEffect(target, "StatusEffectForcedSleeping"))      //are they susceptible?
                {
                    EnsureComp<MindcontrolledComponent>(target, out var flashed);        //Mind control em
                    flashed.Master = component.HolderUid;
                    _mindcontrol.Start(target, flashed);
                }
            }
    }
}

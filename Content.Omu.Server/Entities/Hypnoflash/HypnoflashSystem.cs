using Content.Goobstation.Server.Mindcontrol;
using Content.Goobstation.Shared.Mindcontrol;
using Content.Shared.Flash;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Flash.Components;
using System.Linq;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Drunk;
using Content.Shared.StatusEffectNew;
using Content.Goobstation.Common.Flash;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Content.Shared.Speech.Components;
using Content.Shared.Mind;

namespace Content.Omu.Server.Entities.Hypnoflash;
public sealed class MindcontrolImplantSystem : EntitySystem
{
    [Dependency] private readonly MindcontrolSystem _mindcontrol = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly SharedChargesSystem _sharedCharges = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public static readonly ProtoId<TagPrototype> IgnoreResistancesTag = "FlashIgnoreResistances";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HypnoflashComponent, MeleeHitEvent>(OnFlash);
    }
    private void OnFlash(EntityUid uid, HypnoflashComponent component, MeleeHitEvent args)
    {
        if (!TryComp<FlashComponent>(args.Weapon, out var flashcomp))           //Needs a flash... Duh
            return;

        if (TryComp<LimitedChargesComponent>(args.Weapon, out var charges)
            && _sharedCharges.IsEmpty((args.Weapon, charges)))
            return;

        if (!flashcomp.FlashOnMelee ||                                              //Check if it melee'd something
            !args.IsHit
            || !args.HitEntities.Any()
            || args.HitEntities.Count == 0)
            return;




        foreach (var target in args.HitEntities)
        {
            var vulnerableEv = new CheckFlashVulnerable();
            RaiseLocalEvent(target, ref vulnerableEv);

            if (!_tag.HasTag(args.Weapon, IgnoreResistancesTag)
                && !vulnerableEv.Vulnerable)
            {
                var attempt = new FlashAttemptEvent(target, args.User, args.Weapon);
                RaiseLocalEvent(target, ref attempt, true);

                if (attempt.Cancelled)
                    return;
            }
            if (HasComp<DrunkStatusEffectComponent>(target)
                || _statusEffect.HasStatusEffect(target, "StatusEffectSeeingRainbow")
                || _statusEffect.HasStatusEffect(target, "StatusEffectDrowsiness")
                || _statusEffect.HasStatusEffect(target, "StatusEffectForcedSleeping"))      //are they susceptible?
            {
                EnsureComp<MindcontrolledComponent>(target, out var flashed);        //Mind control em
                flashed.Master = args.User;
                _mindcontrol.Start(target, flashed);

                if (TryComp<MeleeSpeechComponent>(args.Weapon, out var speech)) //Objective larp
                    if (speech.Battlecry is not null)
                    {
                        var objective = speech.Battlecry;
                        AssignObjective(target, objective);
                    }
            }
        }
    }

    private void AssignObjective(EntityUid target, string objective)
    {
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return;

        var xform = Transform(target);
        var objectiveId = PredictedSpawnAtPosition("HypnotizedObjective", xform.Coordinates);
        _meta.SetEntityDescription(objectiveId, objective);
        _mind.AddObjective(mindId, mind, objectiveId);
    }
}

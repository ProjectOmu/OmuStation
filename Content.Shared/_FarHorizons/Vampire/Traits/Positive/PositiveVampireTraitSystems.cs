using System.Linq;
using Content.Shared._EinsteinEngines.Language.Components;
using Content.Shared._EinsteinEngines.Language.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.EntityEffects;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Prying.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._FarHorizons.Vampire.Traits.Positive;

public sealed class
    SupernaturalStrengthVampireTraitSystem : LesserVampireTraitSystem<SupernaturalStrengthVampireTraitComponent>
{
    protected override void TraitInit(Entity<LesserVampireComponent, SupernaturalStrengthVampireTraitComponent> ent) =>
        EnsureComp<PryingComponent>(ent);
}

public sealed class HealingBloodVampireTraitSystem : LesserVampireTraitSystem<HealingBloodVampireTraitComponent>
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    protected override void TraitInit(Entity<LesserVampireComponent, HealingBloodVampireTraitComponent> ent)
    {
        if (!TryComp<BodyComponent>(ent, out var body) ||
            !_body.TryGetBodyOrganEntityComps<StomachComponent>((ent, body), out var stomachs))
            return;


        foreach (var stomachOrgan in stomachs)
        {
            if (!TryComp<MetabolizerComponent>(stomachOrgan, out var stomach))
                continue;

            stomach.MetabolizerTypes?.Add(ent.Comp2.Metabolizer.Id);
        }
    }
}

public sealed class
    LanguageAbsorptionVampireTraitSystem : LesserVampireTraitSystem<LanguageAbsorptionVampireTraitComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedLanguageSystem _language = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LanguageAbsorptionVampireTraitComponent, OnVampireBite>(OnVampireBite);
    }

    private void OnVampireBite(Entity<LanguageAbsorptionVampireTraitComponent> ent, ref OnVampireBite args)
    {
        if (ent.Comp.AlreadyChecked.Contains(args.Target)) return;
        ent.Comp.AlreadyChecked.Add(args.Target);

        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent), GetNetEntity(args.Target));
        if (!random.Prob(ent.Comp.Chance)) return;

        if (!TryComp<LanguageSpeakerComponent>(ent, out var vampireLanguageComponent)
            || !TryComp<LanguageSpeakerComponent>(args.Target, out var targetLanguageComponent))
            return;

        var vampireUnderstood = vampireLanguageComponent.UnderstoodLanguages;
        var targetSpoken = targetLanguageComponent.SpokenLanguages;

        var possibleToLearn = targetSpoken.Except(vampireUnderstood).ToList();
        if (possibleToLearn.Count == 0) return;

        var newLang = random.Pick(possibleToLearn);
        _language.AddLanguage(ent, newLang, addSpoken: false);
    }
}

public sealed class ExtendableFangsVampireTraitSystem : LesserVampireToggleActionTraitSystem<
    ExtendableFangsVampireTraitComponent, VampireFangsRetractEvent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExtendableFangsVampireTraitComponent, VampireFangsCheck>(OnFangsCheck);
        SubscribeLocalEvent<ExtendableFangsVampireTraitComponent, VampireBiteCheck>(OnBiteCheck);
    }

    private void OnBiteCheck(Entity<ExtendableFangsVampireTraitComponent> ent, ref VampireBiteCheck args)
    {
        if (ent.Comp.Toggled)
            args.Cancelled = true;
    }

    private void OnFangsCheck(Entity<ExtendableFangsVampireTraitComponent> ent, ref VampireFangsCheck args)
    {
        if (ent.Comp.Toggled)
            args.FangsHidden = true;
    }
}

public sealed class
    TransfusionVampireTraitSystem : LesserVampireActionTraitSystem<TransfusionVampireTraitComponent,
    VampireTransfusionEvent>
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransfusionVampireTraitComponent, LesserVampireTransfusionDoAfterEvent>(OnDoAfter);
    }

    protected override void ActionUsed(Entity<LesserVampireComponent, TransfusionVampireTraitComponent> ent,
        ref VampireTransfusionEvent args)
    {
        if (!TryComp<BloodstreamComponent>(args.Target, out var bloodstream) ||
            !CanTransfer(ent, (args.Target, bloodstream)))
            return;

        if (ent.Comp2.PopupMessage != null)
            _popup.PopupEntity(
                Loc.GetString(ent.Comp2.PopupMessage, ("vampire", Identity.Entity(ent, EntityManager)),
                    ("target", Identity.Entity(args.Target, EntityManager))), ent, ent, PopupType.Medium);

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, ent, ent.Comp2.DoAfterDuration, new LesserVampireTransfusionDoAfterEvent(),
                eventTarget: ent, target: args.Target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                MovementThreshold = 0.01f,
                DistanceThreshold = 1.0f,
                NeedHand = false
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnDoAfter(Entity<TransfusionVampireTraitComponent> ent, ref LesserVampireTransfusionDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Target == null ||
            !TryComp<LesserVampireComponent>(ent, out var vampire) ||
            !TryComp<BloodstreamComponent>(args.Target, out var bloodstream))
            return;

        var currBlood = Vampire.GetBloodPool((ent, vampire));
        Vampire.SetBloodPool((ent, vampire), currBlood - ent.Comp.DecreaseBloodPool);
        _bloodstream.TryModifyBloodLevel((args.Target.Value, bloodstream), ent.Comp.IncreaseBloodLevel);

        args.Repeat = CanTransfer((ent, vampire, ent.Comp), (args.Target.Value, bloodstream));
    }

    private bool CanTransfer(Entity<LesserVampireComponent, TransfusionVampireTraitComponent> vampire,
        Entity<BloodstreamComponent> target) =>
        _bloodstream.GetBloodLevelPercentage(target.AsNullable()) < 1 &&
        Vampire.GetBloodPool(vampire) >= vampire.Comp2.DecreaseBloodPool;
}

public sealed class
    CharmVampireTraitSystem : LesserVampireToggleActionTraitSystem<CharmVampireTraitComponent, VampireCharmEvent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedEntityEffectSystem _effects = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityManager.AllEntityQueryEnumerator<LesserVampireComponent, CharmVampireTraitComponent>();
        while (query.MoveNext(out var uid, out var vampire, out var comp))
        {
            if (!comp.Toggled) continue;
            if (comp.TickRate == TimeSpan.Zero || _timing.CurTime < comp.NextUpdate) continue;
            comp.NextUpdate = _timing.CurTime + comp.TickRate;

            var mindInRange = _lookup.GetEntitiesInRange(uid, comp.Range).Where(p =>
                HasComp<BloodstreamComponent>(p) && TryComp<MindContainerComponent>(p, out var mind) &&
                _mind.GetMind(p, mind) != null);

            foreach (var sentient in mindInRange)
            {
                var effectArgs = new EntityEffectBaseArgs(sentient, EntityManager);
                foreach (var effect in comp.Effects)
                    if (effect.ShouldApply(effectArgs, _random))
                        _effects.Effect(effect, effectArgs);
            }
        }
    }
}

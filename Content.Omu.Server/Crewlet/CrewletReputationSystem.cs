// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Omu.Shared.Crewlet;
using Content.Shared.Body.Prototypes;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.EntityConditions;
using Content.Shared.EntityConditions.Conditions.Body;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Crewlet;

public sealed class CrewletReputationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CrewletSystem _crewlet = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<MetabolismGroupPrototype> Medicine = "Medicine";
    private static readonly ProtoId<MetabolismGroupPrototype> Poison = "Poison";

    private enum ReagentEffect
    {
        Neutral,
        Helpful,
        Harmful,
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrewletReputationComponent, InteractionSuccessEvent>(OnHugged);
        SubscribeLocalEvent<CrewletReputationComponent, EatingDoAfterEvent>(OnEatingDoAfter,
            before: [typeof(IngestionSystem)]);
        SubscribeLocalEvent<CrewletReputationComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<CrewletReputationComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CrewletReputationComponent, DisarmedEvent>(OnDisarmed);
        SubscribeLocalEvent<CrewletReputationComponent, TargetBeforeInjectEvent>(OnBeforeInject);
        SubscribeLocalEvent<CrewletReputationComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<CrewletReputationComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CrewletOwnerComponent, PolymorphedEvent>(OnOwnerPolymorphed);
    }

    public float GetOpinion(Entity<CrewletReputationComponent?> crewlet, EntityUid person)
    {
        if (!Resolve(crewlet, ref crewlet.Comp, false)
            || !crewlet.Comp.Opinions.TryGetValue(person, out var opinion))
            return 0f;

        return Decayed(crewlet.Comp, opinion);
    }

    private void Adjust(Entity<CrewletReputationComponent> crewlet,
        EntityUid person,
        float delta,
        CrewletOpinionReason reason)
    {
        if (delta == 0f || person == crewlet.Owner || !HasComp<MobStateComponent>(person))
            return;

        var comp = crewlet.Comp;
        Prune(comp);

        var opinion = Math.Clamp(GetOpinion(crewlet.AsNullable(), person) + delta, -comp.MaxOpinion, comp.MaxOpinion);
        comp.Opinions[person] = new CrewletOpinion(opinion, _timing.CurTime);

        var ev = new CrewletOpinionChangedEvent(person, opinion, reason);
        RaiseLocalEvent(crewlet, ref ev);

        if (!TryComp<CrewletComponent>(crewlet, out var follower))
            return;

        if (delta < 0f && follower.CrewletOwner == person && opinion <= comp.AbandonThreshold)
            Abandon((crewlet.Owner, comp, follower), person);
        else if (delta > 0f && follower.CrewletOwner == null && opinion >= comp.AdoptThreshold && CanAdopt(person))
            ChangeOwner((crewlet.Owner, follower), null, person);
    }

    private void OnHugged(Entity<CrewletReputationComponent> ent, ref InteractionSuccessEvent args)
    {
        if (Ready(ent.Comp.NextHug, args.User, ent.Comp.HugCooldown))
            Adjust(ent, args.User, ent.Comp.HugOpinion, CrewletOpinionReason.Hugged);
    }

    private void OnEatingDoAfter(Entity<CrewletReputationComponent> ent, ref EatingDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        ent.Comp.Feeding = new CrewletTally { Person = args.User, Tick = _timing.CurTick };
    }

    private void OnIngesting(Entity<CrewletReputationComponent> ent, ref IngestingEvent args)
    {
        if (ent.Comp.Feeding.Tick != _timing.CurTick)
            return;

        var feeder = ent.Comp.Feeding.Person;
        ent.Comp.Feeding = default;

        var poisoned = args.Split.Contents
            .Select(reagent => _proto.Index<ReagentPrototype>(reagent.Reagent.Prototype))
            .Any(reagent => Classify(reagent) == ReagentEffect.Harmful);

        if (poisoned)
            Adjust(ent, feeder, ent.Comp.PoisonOpinion, CrewletOpinionReason.FedPoison);
        else if (Ready(ent.Comp.NextFed, feeder, ent.Comp.FedCooldown))
            Adjust(ent, feeder, ent.Comp.FedOpinion, CrewletOpinionReason.Fed);
    }

    private void OnDamageChanged(Entity<CrewletReputationComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } delta
            || args.Origin is not { } origin
            || FindMob(origin) is not { } person)
            return;

        var dealt = 0f;
        var healed = 0f;
        foreach (var amount in delta.DamageDict.Values)
        {
            var value = amount.Float();
            if (value > 0f)
                dealt += value;
            else
                healed -= value;
        }

        if (args.DamageIncreased)
        {
            ref var hurt = ref Track(ref ent.Comp.Hurt, person);
            hurt.Total += dealt;
            var penalty = MathF.Max(hurt.Total * ent.Comp.HurtPerDamage, ent.Comp.MinHurtPenalty);
            Settle(ent, ref hurt, -penalty, CrewletOpinionReason.Hurt);
        }
        else if (healed > 0f)
        {
            ref var heal = ref Track(ref ent.Comp.Healed, person);
            heal.Total += healed;
            var bonus = MathF.Min(heal.Total * ent.Comp.HealedPerDamage, ent.Comp.MaxHealedBonus);
            Settle(ent, ref heal, bonus, CrewletOpinionReason.Healed);
        }
    }

    private void OnDisarmed(Entity<CrewletReputationComponent> ent, ref DisarmedEvent args)
    {
        Adjust(ent, args.Source, ent.Comp.ShovedOpinion, CrewletOpinionReason.Shoved);
    }

    private void OnBeforeInject(Entity<CrewletReputationComponent> ent, ref TargetBeforeInjectEvent args)
    {
        if (args.Cancelled)
            return;

        ent.Comp.Injection = new CrewletTally { Person = args.EntityUsingInjector, Tick = _timing.CurTick };
    }

    private void OnReaction(Entity<CrewletReputationComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Injection || ent.Comp.Injection.Tick != _timing.CurTick)
            return;

        var applied = ent.Comp.Injection.Applied;
        var target = Classify(args.Reagent) switch
        {
            ReagentEffect.Harmful => ent.Comp.PoisonOpinion,
            ReagentEffect.Helpful when applied != ent.Comp.PoisonOpinion => ent.Comp.MedicineOpinion,
            _ => applied,
        };

        if (target == ent.Comp.MedicineOpinion
            && applied != target
            && !Ready(ent.Comp.NextMedicine, ent.Comp.Injection.Person, ent.Comp.MedicineCooldown))
            return;

        var reason = target == ent.Comp.PoisonOpinion
            ? CrewletOpinionReason.InjectedPoison
            : CrewletOpinionReason.Medicated;

        Settle(ent, ref ent.Comp.Injection, target, reason);
    }

    private void OnExamined(Entity<CrewletReputationComponent> ent, ref ExaminedEvent args)
    {
        var opinion = GetOpinion(ent.AsNullable(), args.Examiner);
        if (opinion == 0f)
            return;

        var strong = MathF.Abs(opinion) >= ent.Comp.MaxOpinion * ent.Comp.StrongFeeling;
        var text = opinion > 0f
            ? strong ? "crewlet-examine-loves" : "crewlet-examine-likes"
            : strong ? "crewlet-examine-hates" : "crewlet-examine-dislikes";

        var message = new FormattedMessage();
        message.PushColor(opinion > 0f ? ent.Comp.LikeColor : ent.Comp.DislikeColor);
        message.AddText(Loc.GetString(text, ("crewlet", ent.Owner)));
        message.Pop();
        args.PushMessage(message);
    }

    private void OnOwnerPolymorphed(Entity<CrewletOwnerComponent> ent, ref PolymorphedEvent args)
    {
        if (args.OldEntity != ent.Owner)
            return;

        foreach (var uid in ent.Comp.Crewlets.ToArray())
        {
            if (TryComp<CrewletReputationComponent>(uid, out var reputation)
                && reputation.Opinions.Remove(args.OldEntity, out var opinion))
                reputation.Opinions[args.NewEntity] = opinion;

            if (TryComp<CrewletComponent>(uid, out var crewlet))
                _crewlet.SetOwner((uid, crewlet), args.NewEntity);
        }
    }

    private void Abandon(Entity<CrewletReputationComponent, CrewletComponent> crewlet, EntityUid owner)
    {
        var origin = _transform.GetMapCoordinates(crewlet.Owner);
        EntityUid? favorite = null;
        var favoriteOpinion = 0f;
        EntityUid? nearest = null;
        var nearestDistance = float.MaxValue;

        var people = _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(origin, crewlet.Comp1.AdoptRange);
        foreach (var person in people)
        {
            if (person.Owner == owner || !CanAdopt(person))
                continue;

            var opinion = GetOpinion((crewlet.Owner, crewlet.Comp1), person);
            if (opinion >= crewlet.Comp1.AdoptThreshold && (favorite == null || opinion > favoriteOpinion))
            {
                favorite = person;
                favoriteOpinion = opinion;
            }

            var distance = (_transform.GetMapCoordinates(person).Position - origin.Position).Length();
            if (opinion >= 0f && distance < nearestDistance)
            {
                nearest = person;
                nearestDistance = distance;
            }
        }

        ChangeOwner((crewlet.Owner, crewlet.Comp2), owner, favorite ?? nearest);
    }

    private void ChangeOwner(Entity<CrewletComponent> crewlet, EntityUid? previous, EntityUid? adopter)
    {
        _crewlet.SetOwner(crewlet, adopter);

        var name = Identity.Entity(crewlet, EntityManager);
        if (previous is { } abandoned)
        {
            _popup.PopupEntity(Loc.GetString("crewlet-abandoned", ("crewlet", name)),
                crewlet,
                abandoned,
                PopupType.MediumCaution);
        }

        if (adopter is { } adopted)
            _popup.PopupEntity(Loc.GetString("crewlet-adopted", ("crewlet", name)), crewlet, adopted, PopupType.Medium);
    }

    private bool CanAdopt(EntityUid person)
    {
        return HasComp<HumanoidAppearanceComponent>(person)
            && !HasComp<CrewletComponent>(person)
            && _mobState.IsAlive(person);
    }

    private EntityUid? FindMob(EntityUid uid)
    {
        var current = uid;
        while (TryComp(current, out TransformComponent? xform))
        {
            if (HasComp<MobStateComponent>(current))
                return current;

            current = xform.ParentUid;
        }

        return null;
    }

    private bool Ready(Dictionary<EntityUid, TimeSpan> next, EntityUid person, TimeSpan cooldown)
    {
        var now = _timing.CurTime;
        if (next.TryGetValue(person, out var at) && now < at)
            return false;

        next[person] = now + cooldown;
        return true;
    }

    private ref CrewletTally Track(ref CrewletTally tally, EntityUid person)
    {
        if (tally.Person != person || tally.Tick != _timing.CurTick)
            tally = new CrewletTally { Person = person, Tick = _timing.CurTick };

        return ref tally;
    }

    private void Settle(Entity<CrewletReputationComponent> ent,
        ref CrewletTally tally,
        float target,
        CrewletOpinionReason reason)
    {
        var delta = target - tally.Applied;
        tally.Applied = target;
        Adjust(ent, tally.Person, delta, reason);
    }

    private ReagentEffect Classify(ReagentPrototype reagent)
    {
        if (reagent.Metabolisms is not { } metabolisms)
            return ReagentEffect.Neutral;

        var changes = metabolisms.Values
            .SelectMany(entry => entry.Effects)
            .OfType<HealthChange>()
            .ToList();
        var damage = changes
            .Where(health => AppliesToMostBodies(health.Conditions))
            .Sum(health => health.Damage.GetTotal().Float());

        if (damage > 0f)
            return ReagentEffect.Harmful;

        if (damage < 0f)
            return ReagentEffect.Helpful;

        var medicine = metabolisms.ContainsKey(Medicine);
        var poison = metabolisms.ContainsKey(Poison);
        if (medicine && !poison)
            return ReagentEffect.Helpful;

        return poison && !medicine && changes.Count == 0 ? ReagentEffect.Harmful : ReagentEffect.Neutral;
    }

    private static bool AppliesToMostBodies(EntityCondition[]? conditions)
    {
        if (conditions == null)
            return true;

        foreach (var condition in conditions)
        {
            if (condition is not MetabolizerTypeCondition { Inverted: true })
                return false;
        }

        return true;
    }

    private float Decayed(CrewletReputationComponent comp, CrewletOpinion opinion)
    {
        var decay = comp.DecayPerMinute * (float) (_timing.CurTime - opinion.Updated).TotalMinutes;
        return MathF.Sign(opinion.Value) * MathF.Max(MathF.Abs(opinion.Value) - decay, 0f);
    }

    private void Prune(CrewletReputationComponent comp)
    {
        foreach (var (person, opinion) in comp.Opinions)
        {
            if (TerminatingOrDeleted(person) || Decayed(comp, opinion) == 0f)
                comp.Opinions.Remove(person);
        }

        PruneCooldowns(comp.NextHug);
        PruneCooldowns(comp.NextFed);
        PruneCooldowns(comp.NextMedicine);
    }

    private void PruneCooldowns(Dictionary<EntityUid, TimeSpan> next)
    {
        var now = _timing.CurTime;
        foreach (var (person, at) in next)
        {
            if (TerminatingOrDeleted(person) || at <= now)
                next.Remove(person);
        }
    }
}

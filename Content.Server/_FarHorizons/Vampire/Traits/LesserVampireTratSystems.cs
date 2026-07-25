using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Shared._FarHorizons.Vampire;
using Content.Shared._FarHorizons.Vampire.Traits;
using Content.Shared._FarHorizons.Vampire.Traits.Negative;
using Content.Shared._FarHorizons.Vampire.Traits.Positive;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._FarHorizons.Vampire.Traits;

// This had to be on server because it's dealing with grids
public sealed class
    UvSensitivityVampireTraitSystem : LesserVampirePassiveTraitSystem<UvSensitivityVampireTraitComponent>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void UpdateEffect(Entity<LesserVampireComponent, UvSensitivityVampireTraitComponent> ent)
    {
        var transform = Transform(ent);

        var uvProtected = _transform.GetGrid(transform.Coordinates) is { } grid &&
                          (HasComp<BecomesStationComponent>(grid) ||
                           HasComp<StationMemberComponent>(grid) ||
                           HasComp<MapAtmosphereComponent>(grid) ||
                           HasComp<EmergencyShuttleComponent>(grid));

        var valBefore = ent.Comp2.CurrentlyDrained;
        ent.Comp2.CurrentlyDrained = !uvProtected;

        if (valBefore != ent.Comp2.CurrentlyDrained)
            Vampire.RefreshBloodPoolChange(ent);
    }

    protected override void RefreshBloodpoolDrain(
        Entity<LesserVampireComponent, UvSensitivityVampireTraitComponent> ent, ref GetVampireBloodPoolChange args)
    {
        base.RefreshBloodpoolDrain(ent, ref args);

        if (ent.Comp2.CurrentlyDrained)
            args.Change -= ent.Comp2.BloodPoolDrain;
    }
}

// Making vampire is server side also
public sealed class
    ConversionVampireTraitSystem : LesserVampireActionTraitSystem<ConversionVampireTraitComponent,
    VampireConversionEvent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private EntProtoId _sleepStatus = "StatusEffectForcedSleeping";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConversionVampireTraitComponent, LesserVampireConversionDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<VampireConversionCandidateComponent, VampireConversionAcceptEvent>(OnAcceptConversion);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityManager.AllEntityQueryEnumerator<VampireConversionCandidateComponent>();
        while (query.MoveNext(out var uid, out var candidate))
        {
            if (!candidate.Accepted ||
                _timing.CurTime < candidate.NextUpdate ||
                candidate.ConvertedBy == null ||
                _mobState.IsDead(uid) ||
                !TryComp<LesserVampireComponent>(candidate.ConvertedBy.Value, out var parentVampire))
                continue;
            candidate.NextUpdate = _timing.CurTime + candidate.UpdateRate;

            if (_timing.CurTime >= candidate.ConvertAt)
            {
                if (candidate.DoConvert)
                {
                    var vampire = CopyComp(candidate.ConvertedBy.Value, uid, parentVampire);
                    Vampire.RefreshBloodPoolChange((uid, vampire));
                }
                RemCompDeferred<VampireConversionCandidateComponent>(uid);
                _sleeping.TryWaking(uid, true);
                continue;
            }

            var healing = candidate.ConvertedBy.Value.Comp.ComaHealing;
            _damageable.TryChangeDamage(uid, healing, true, false);
        }
    }

    private void OnAcceptConversion(Entity<VampireConversionCandidateComponent> ent, ref VampireConversionAcceptEvent args)
    {
        if (ent.Comp.Accepted ||
            ent.Comp.ConvertedBy == null ||
            _mobState.IsDead(ent) ||
            !_statusEffect.TryAddStatusEffectDuration(ent, _sleepStatus, ent.Comp.ConvertedBy.Value.Comp.ConversionTime))
            return;

        ent.Comp.Accepted = true;
        ent.Comp.ConvertAt = _timing.CurTime + ent.Comp.ConvertedBy.Value.Comp.ConversionTime;

        Actions.RemoveAction(ent.Owner, args.Action.AsNullable());
    }

    private void OnDoAfter(Entity<ConversionVampireTraitComponent> ent, ref LesserVampireConversionDoAfterEvent args)
    {
        if (ent.Comp.Used ||
            args.Cancelled ||
            args.Target == null ||
            !TryComp<LesserVampireComponent>(ent, out var vampire))
            return;

        var currBlood = Vampire.GetBloodPool((ent, vampire));
        Vampire.SetBloodPool((ent, vampire), currBlood - ent.Comp.DecreaseBloodPool);
        if (ent.Comp.SingleUse)
            ent.Comp.Used = true;

        var comp = EnsureComp<VampireConversionCandidateComponent>(args.Target.Value);
        comp.ConvertedBy = ent;
        comp.DoConvert = ent.Comp.Convert;
        Actions.AddAction(args.Target.Value, ent.Comp.AcceptAction);

        var action = Actions.GetActions(ent)
            .Where(p => MetaData(p).EntityPrototype is { } entProto && entProto.ID == ent.Comp.Action)
            .FirstOrNull();
        if (action != null && ent.Comp.SingleUse)
            Actions.RemoveAction(ent.Owner, action.Value.AsNullable());
    }

    protected override void ActionUsed(Entity<LesserVampireComponent, ConversionVampireTraitComponent> ent, ref VampireConversionEvent args)
    {
        if (ent.Comp2.Used ||
            !TryComp<MindContainerComponent>(args.Target, out var mind) ||
            _mind.GetMind(args.Target, mind) == null ||
            _mobState.IsDead(args.Target) ||
            Vampire.GetBloodPool(ent) < ent.Comp2.DecreaseBloodPool ||
            HasComp<VampireConversionCandidateComponent>(args.Target) ||
            _tag.HasAnyTag(args.Target, ent.Comp2.BlacklistTargets))
            return;

        if (ent.Comp2.PopupMessage != null)
            _popup.PopupEntity(
                Loc.GetString(ent.Comp2.PopupMessage, ("vampire", Identity.Entity(ent, EntityManager)),
                    ("target", Identity.Entity(args.Target, EntityManager))), ent, ent, PopupType.Medium);

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, ent, ent.Comp2.DoAfterDuration, new LesserVampireConversionDoAfterEvent(),
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
}
using System.Linq;
using Content.Shared._Shitmed.CCVar;
using Content.Shared._Shitmed.Medical.Surgery.Pain;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body.Organ;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Rejuvenate; // Omu
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

public partial class TraumaSystem
{
    private const string OrganDamagePainIdentifier = "OrganDamage";
    public static readonly EntProtoId OrgansDamagedSlowdown = "OrgansDamagedSlowdownEffect";

    private void InitOrgans()
    {
        SubscribeLocalEvent<WoundableComponent, OrganIntegrityChangedEventOnWoundable>(OnOrganIntegrityOnWoundableChanged);
        SubscribeLocalEvent<OrganComponent, OrganIntegrityChangedEvent>(OnOrganIntegrityChanged);
        SubscribeLocalEvent<WoundableComponent, OrganDamageSeverityChangedOnWoundable>(OnOrganSeverityChanged);
    }

    #region Event handling

    private void OnOrganIntegrityOnWoundableChanged(Entity<WoundableComponent> bodyPart, ref OrganIntegrityChangedEventOnWoundable args)
    {
        if (args.Organ.Comp.Body == null)
            return;

        if (!_consciousness.TryGetNerveSystem(args.Organ.Comp.Body.Value, out var nerveSys))
            return;

        var organs = _body.GetPartOrgans(args.Organ.Comp.Body.Value).ToList();
        var totalIntegrity = organs.Aggregate(FixedPoint2.Zero, (current, organ) => current + organ.Component.OrganIntegrity);
        var totalIntegrityCap = organs.Aggregate(FixedPoint2.Zero, (current, organ) => current + organ.Component.IntegrityCap);
        // Getting your organ turned into a blood mush inside you applies a LOT of internal pain, that can get you dead.
        if (!_pain.TryChangePainModifier(
                nerveSys.Value,
                bodyPart.Owner,
                OrganDamagePainIdentifier,
                (totalIntegrityCap - totalIntegrity) / 2,
                nerveSys.Value.Comp))
        {
            _pain.TryAddPainModifier(
                nerveSys.Value,
                bodyPart.Owner,
                OrganDamagePainIdentifier,
                (totalIntegrityCap - totalIntegrity) / 2,
                PainDamageTypes.TraumaticPain,
                nerveSys.Value.Comp);
        }
    }

    private void OnOrganIntegrityChanged(Entity<OrganComponent> organ, ref OrganIntegrityChangedEvent args)
    {
        if (organ.Comp.Body == null)
            return;

        if (args.NewIntegrity < organ.Comp.IntegrityCap || !TryGetBodyTraumas(organ.Comp.Body.Value, out var traumas, TraumaType.OrganDamage))
            return;

        foreach (var trauma in traumas.Where(trauma => trauma.Comp.TraumaTarget == organ))
        {
            RemoveTrauma(trauma);
        }
    }

    private void OnOrganSeverityChanged(Entity<WoundableComponent> bodyPart, ref OrganDamageSeverityChangedOnWoundable args)
    {
        var body = args.Organ.Comp.Body;
        if (body == null
            || args.NewSeverity < args.OldSeverity)
            return;

        _popup.PopupClient(Loc.GetString($"popup-trauma-OrganDamage-{args.NewSeverity.ToString()}", ("part", bodyPart)),
            body.Value,
            body.Value,
            PopupType.SmallCaution);

        if (args.NewSeverity != OrganSeverity.Destroyed)
            return;

        if (_consciousness.TryGetNerveSystem(body.Value, out var nerveSys)
            && !_mobState.IsDead(body.Value))
        {
            var sex = Sex.Unsexed;
            if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
                sex = humanoid.Sex;

            _pain.PlayPainSoundWithCleanup(
                body.Value,
                nerveSys.Value.Comp,
                nerveSys.Value.Comp.OrganDestructionReflexSounds[sex],
                AudioParams.Default.WithVolume(6f));

            _stun.TryUpdateParalyzeDuration(body.Value, nerveSys.Value.Comp.OrganDamageStunTime);
            _movementMod.TryUpdateMovementSpeedModDuration(
                 body.Value,
                 OrgansDamagedSlowdown,
                 nerveSys.Value.Comp.OrganDamageStunTime * _cfg.GetCVar(SurgeryCVars.OrganTraumaSlowdownTimeMultiplier),
                 _cfg.GetCVar(SurgeryCVars.OrganTraumaWalkSpeedSlowdown),
                 _cfg.GetCVar(SurgeryCVars.OrganTraumaRunSpeedSlowdown));
        }

        if (TryGetWoundableTrauma(bodyPart, out var traumas, TraumaType.OrganDamage, bodyPart))
        {
            foreach (var trauma in traumas)
            {
                if (trauma.Comp.TraumaTarget != args.Organ)
                    continue;

                RemoveTrauma(trauma);
            }
        }

        _audio.PlayPvs(args.Organ.Comp.OrganDestroyedSound, body.Value);
        _body.RemoveOrgan(args.Organ, args.Organ.Comp);

        if (_net.IsServer)
            QueueDel(args.Organ);
    }

    #endregion

    #region Public API

    // Omu Start
    /// <summary>
    /// Let this be the defacto way of applying damage to organIntegrity.
    /// </summary>
    /// <param name="uid"> The organ entity's uid </param>
    /// <param name="severity"> The amount of damage to be dealt </param>
    /// <param name="effectOwner"> Who applied the effect (used for debugging idk) </param>
    /// <param name="identifier"> Unique string??? </param>
    /// <param name="organ"> The organ component of the organ entity </param>
    /// <returns> True if damage was successfully added to the organ's modifier list. </returns>
    /// <example> _trauma.TryMakeOrganDamageModifier(eye.Owner, amount, blindable.Owner, "BlindableDamage", eye.Comp2)</example>
    /// <remarks>This is the biggest piece of shit I had ever seen. Halfassed implementation for public API that never gets used and had to be REDONE in EYESSYSTEM! AND EVEN THAT DIDN'T WORK OR DO ANYTHING</remarks>
    public bool TryMakeOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (severity == 0
            || !Resolve(uid, ref organ))
            return false;
        // As I am obfuscating cumbersome methods here
#pragma warning disable CS0618
        if (!TryCreateOrganDamageModifier(uid, severity, effectOwner, identifier, organ))
        {
            if (!TryChangeOrganDamageModifier(uid, severity, effectOwner, identifier, organ))
                return false;
        }
        return true;
    }
#pragma warning restore CS0618
    [Obsolete("In most cases, use TryMakeOrganDamageModifier() instead")]
    // Omu End
    public bool TryCreateOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (severity == 0
            || !Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.TryAdd((identifier, effectOwner), severity))
            return false;

        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TrySetOrganDamageModifier(EntityUid uid,
        FixedPoint2 severity,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (severity == 0
            || !Resolve(uid, ref organ))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = severity;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    [Obsolete("In most cases, use TryMakeOrganDamageModifier() instead")] // Omu
    public bool TryChangeOrganDamageModifier(EntityUid uid,
        FixedPoint2 change,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (change == 0
            || !Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.TryGetValue((identifier, effectOwner), out var value))
            return false;

        organ.IntegrityModifiers[(identifier, effectOwner)] = value + change;
        UpdateOrganIntegrity(uid, organ);

        return true;
    }

    public bool TryRemoveOrganDamageModifier(EntityUid uid,
        EntityUid effectOwner,
        string identifier,
        OrganComponent? organ = null)
    {
        if (!Resolve(uid, ref organ))
            return false;

        if (!organ.IntegrityModifiers.Remove((identifier, effectOwner)))
            return false;

        if (TryComp<TraumaComponent>(effectOwner, out var traumaComp))
            RemoveTrauma((effectOwner, traumaComp));

        UpdateOrganIntegrity(uid, organ);
        return true;
    }

    #endregion

    #region Private API

    private void UpdateOrganIntegrity(EntityUid uid, OrganComponent organ)
    {
        var oldIntegrity = organ.OrganIntegrity;

        if (organ.IntegrityModifiers.Count > 0)
            organ.OrganIntegrity = organ.IntegrityCap - FixedPoint2.Clamp(organ.IntegrityModifiers // Omu
                .Aggregate(FixedPoint2.Zero, (current, modifier) => current + modifier.Value),
                0,
                organ.IntegrityCap);

        if (oldIntegrity != organ.OrganIntegrity)
        {
            var ev = new OrganIntegrityChangedEvent(oldIntegrity, organ.OrganIntegrity);
            RaiseLocalEvent(uid, ref ev);

            if (_container.TryGetContainingContainer((uid, Transform(uid), MetaData(uid)), out var container))
            {
                var ev1 = new OrganIntegrityChangedEventOnWoundable((uid, organ), oldIntegrity, organ.OrganIntegrity);
                RaiseLocalEvent(container.Owner, ref ev1);
            }
        }

        var nearestSeverity = organ.OrganSeverity;
        foreach (var (severity, value) in organ.IntegrityThresholds.OrderByDescending(kv => kv.Value))
        {
            if (organ.OrganIntegrity > value)
                continue;

            nearestSeverity = severity;
            break;
        }

        if (nearestSeverity != organ.OrganSeverity)
        {
            var ev = new OrganDamageSeverityChanged(organ.OrganSeverity, nearestSeverity);
            RaiseLocalEvent(uid, ref ev);
            if (_container.TryGetContainingContainer((uid, Transform(uid), MetaData(uid)), out var container))
            {
                var ev1 = new OrganDamageSeverityChangedOnWoundable((uid, organ), organ.OrganSeverity, nearestSeverity);
                RaiseLocalEvent(container.Owner, ref ev1);
            }
        }

        organ.OrganSeverity = nearestSeverity;
        Dirty(uid, organ);
    }

    #endregion
}

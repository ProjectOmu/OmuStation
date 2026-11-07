using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Content.Shared._Omu.Traits;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Content.Server.Roles.Jobs;
using Content.Server.Mind;
using Content.Server.Hands.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Tag;
using Content.Server.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.Whitelist;

namespace Content.Server._Omu.Traits;

/// <summary>
///     Used for traits that remove a component upon a player spawning in.
/// </summary>
/// <remarks>
///     taken from EE.
/// </remarks>
public sealed partial class TraitRemoveComponent : TraitFunction
{
    [DataField(required: true), AlwaysPushInheritance]
    public ComponentRegistry ComponentsToRemove { get; private set; } = new();

    public override void OnPlayerSpawn(EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        foreach (var (name, _) in ComponentsToRemove)
            entityManager.RemoveComponentDeferred(uid, factory.GetComponent(name).GetType());
    }
}

/// <remarks>
///     taken from EE.
/// </remarks>
public sealed partial class TraitModifyDensity : TraitFunction
{
    [DataField(required: true), AlwaysPushInheritance]
    public float DensityModifier;

    [DataField, AlwaysPushInheritance]
    public bool? Multiply;

    public override void OnPlayerSpawn(EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        var physicsSystem = entityManager.System<SharedPhysicsSystem>();
        if (!entityManager.TryGetComponent<FixturesComponent>(uid, out var fixturesComponent)
            || fixturesComponent.Fixtures.Count is 0)
            return;

        var fixture = fixturesComponent.Fixtures["fix1"]; // As of writing, fix1 is the fixture used for practically everything.
        var newDensity = (Multiply ?? false) ? fixture.Density * DensityModifier : fixture.Density + DensityModifier;
        physicsSystem.SetDensity(uid, "fix1", fixture, newDensity);
        // SetDensity handles the Dirty.
    }
}

/// <summary>
///     Used for traits that modify unarmed damage on MeleeWeaponComponent.
/// </summary>
/// <remarks>
///     taken from EE.
/// </remarks>
public sealed partial class TraitModifyUnarmed : TraitFunction
{
    // <summary>
    //     The sound played on hitting targets.
    // </summary>
    [DataField, AlwaysPushInheritance]
    public SoundSpecifier? HitSound;

    // <summary>
    //     The animation to play on hit, for both light and power attacks.
    // </summary>
    [DataField, AlwaysPushInheritance]
    public EntProtoId? Animation;

    // <summary>
    //     Whether to set the power attack animation to be the same as the light attack.
    // </summary>
    [DataField, AlwaysPushInheritance]
    public bool? HeavyAnimationFromLight;

    // <summary>
    //     The damage values of unarmed damage.
    // </summary>
    [DataField, AlwaysPushInheritance]
    public DamageSpecifier? Damage;

    // <summary>
    //     Additional damage added to the existing damage.
    // </summary>
    [DataField, AlwaysPushInheritance]
    public DamageSpecifier? FlatDamageIncrease;

    // <summary>
    //     What to multiply the melee weapon range by.
    // </summary>
    [DataField, AlwaysPushInheritance]
    public float? RangeModifier;

    // <summary>
    //     What to multiply the attack rate by.
    // </summary>
    [DataField, AlwaysPushInheritance]
    public float? AttackRateModifier;

    public override void OnPlayerSpawn(EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        if (!entityManager.TryGetComponent<MeleeWeaponComponent>(uid, out var melee))
            return;

        if (HitSound != null)
            melee.HitSound = HitSound;

        if (Animation != null)
            melee.Animation = Animation.Value;

        if (HeavyAnimationFromLight ?? true)
            melee.WideAnimation = melee.Animation;

        if (Damage != null)
            melee.Damage = Damage;

        if (FlatDamageIncrease != null)
            melee.Damage += FlatDamageIncrease;

        if (RangeModifier != null)
            melee.Range *= RangeModifier.Value;

        if (AttackRateModifier != null)
            melee.AttackRate *= AttackRateModifier.Value;

        entityManager.Dirty(uid, melee);
    }
}

/// <summary>
///     Gives the specified equipment to the player on spawn if they have the specified jobs.
/// </summary>
/// <remarks>
///     Made for Cybernetic Beasts, in order to give work mantles to players with jobs that may require them round-start.
/// </remarks>
public sealed partial class TraitGiveEquipmentIfHasJobs : TraitFunction
{

    private JobSystem? _jobSystem;
    private MindSystem? _mindSystem;
    private HandsSystem? _handsSystem;

    /// <summary>
    ///     A list of jobs and the associated equipment which should be given to the player if they have the specified job.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, EntProtoId> JobEquipment;

    /// <summary>
    ///     Equipment that is given to the player, if they do not have any job from the dictionary.
    /// </summary>
    [DataField("equipmentUnmet")]
    public EntProtoId? EquipmentIfRequirementUnmet;

    public override void OnPlayerSpawn(EntityUid uid,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        _mindSystem = entityManager.SystemOrNull<MindSystem>();
        _jobSystem = entityManager.SystemOrNull<JobSystem>();
        _handsSystem = entityManager.SystemOrNull<HandsSystem>();

        if (_mindSystem == null || _jobSystem == null || _handsSystem == null)
            return;

        // Try to get the player's job, and make sure that the player has a transform component so that we can give them the equipment.
        if (_mindSystem.TryGetMind(uid, out var mindId, out var _) &&
            _jobSystem.MindTryGetJob(mindId, out var jobProto) &&
            entityManager.TryGetComponent<TransformComponent>(uid, out var transform))
        {
            EntityUid equipment;

            // If the player has a job which is present in the JobEquipment dictionary, give them their job's respective equipment.
            // Else, give them the default trait equipment (should the default trait equpment exist)
            if (JobEquipment.TryGetValue(jobProto.ID, out var equipmentForJob))
            {
                equipment = entityManager.SpawnEntity(equipmentForJob, transform.Coordinates);
            }
            else if (EquipmentIfRequirementUnmet != null)
            {
                equipment = entityManager.SpawnEntity(EquipmentIfRequirementUnmet, transform.Coordinates);
            }
            else
            {
                return;
            }

            _handsSystem.PickupOrDrop(uid, equipment);
        }
    }
}

/// <summary>
/// Applies the vampiric trait by modifying the player's stomach type.
/// </summary>
/// <remarks>
/// This will override the MetabolizerTypes and SpecialDigestible settings on the player's stomach, allowing for it to keep species-unique organs.
/// </remarks>
public sealed partial class TraitVampire : TraitFunction
{
    private SharedBodySystem? _bodySystem;
    public override void OnPlayerSpawn(
        EntityUid mob,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager)
    {
        _bodySystem = entityManager.SystemOrNull<SharedBodySystem>();

        if (_bodySystem == null || !_bodySystem.TryGetBodyOrganEntityComps<StomachComponent>(mob, out var stomachs))
            return;

        //TryGetBodyOrganEntityComps returns a list of organs. There should only be one but we iterate over it to be safe.
        foreach (var ent in stomachs)
        {
            if (!entityManager.TryGetComponent<MetabolizerComponent>(ent, out var metabolizerComp))
                return;

            var stomachComp = entityManager.GetComponent<StomachComponent>(ent);

            stomachComp.IsSpecialDigestibleExclusive = true;
            stomachComp.SpecialDigestible = new EntityWhitelist { Tags = new() { "Bloodpack" } };

            metabolizerComp.MetabolizerTypes = new() { "Vampire" };

            entityManager.Dirty(ent, stomachComp);
        }
    }
}

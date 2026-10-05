// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Trauma.Tackle;

/// <summary>
/// Added to special equipment or mobs to allow tackles
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TackleModifierComponent : Component
{
    /// <summary>
    /// Multiplier to tackle throw speed
    /// </summary>
    [DataField]
    public float SpeedMultiplier = .75f; //Omu 1 -> .75

    /// <summary>
    /// Multiplier to tackle throw range
    /// </summary>
    [DataField]
    public float RangeMultiplier = .5f; //Omu - 1 -> .5

    /// <summary>
    /// Multiplier to knockdown time when performing tackle
    /// </summary>
    [DataField]
    public float KnockdownTimeMultiplier = 1f;

    /// <summary>
    /// Multiplier to stamina cost of tackle
    /// </summary>
    [DataField]
    public float StaminaCostMultiplier = 1f;

    /// <summary>
    /// The higher this is, the more velocity is relevant when calculating modifiers during tackle collision
    /// </summary>
    [DataField]
    public float SpeedModMultiplier = 0.2f; // Omu - 0.4 -> 0.2

    /// <summary>
    /// Minimal "safe" distance, if tackle collision happens below safe range, user will be hurt
    /// </summary>
    [DataField]
    public float MinDistance = .5f; //Omu N/A -> .5

    /// <summary>
    /// How relevant is stamina damage resistance on target. Higher = more relevant
    /// </summary>
    [DataField]
    public float StamResistModifier = 5f; // Omu - 4 -> 5

    /// <summary> // Omu - kill
    /// If result modifier exceeds this value, target will be disarmed on knockdown
    /// </summary>
    //[DataField]
    //public float DisarmThreshold = 3f; // Omu 1.5 -> 3

    /// <summary>
    /// Bonus modifier to user tackle as well as armor knockdown gates
    /// Each round int represtents 10% more stamina resistance that tackle can overcome
    /// </summary>
    [DataField]
    public float SkillMod;

    /// <summary>
    /// If true, user will grab target on successful tackle outcome
    /// Omu - is currently unused but left for possible future use or admins messing around
    /// </summary>
    [DataField]
    public bool GrabOnSuccess;

    /// <summary>
    /// Modifier to how much damage/paralyze time will the user suffer from when hitting a wall
    /// </summary>
    [DataField]
    public float SeverityModifier = 0.3f; // Omu .2 -> .3

    /// <summary>
    /// Base damage when hitting a wall, multiplier by severity that is dependent on velocity
    /// </summary>
    [DataField]
    public DamageSpecifier BaseUserDamage = new()
    {
        DamageDict =
        {
            { "Blunt", 20 },
        },
    };

    /// <summary>
    /// Base time the user will be knocked on tackle collision
    /// </summary>
    [DataField]
    public float BaseUserKnockdownTime = 1f;

    /// <summary>
    /// Base stamina damage target will receive on collision
    /// </summary>
    [DataField]
    public float BaseTargetStaminaDamage = 20f; //Omu - 22 -> 20

    /// <summary>
    /// Base knockdown time of target during collision
    /// </summary>
    [DataField]
    public float BaseTargetKnockdownTime = 1.25f; // Omu 2 -> 1.25
}

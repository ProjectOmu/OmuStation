// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Clothing;

/// <summary>
/// Modifies speed when worn and activated.
/// Supports <see cref="ItemToggleComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(ClothingSpeedModifierSystem))]
public sealed partial class ClothingSpeedModifierComponent : Component
{
    [DataField]
    public float WalkModifier = 1.0f;

    [DataField]
    public float SprintModifier = 1.0f;

    /// <summary>
    /// Defines if the speed modifier requires <see cref="ItemToggleComponent"/> activation to apply.
    /// This will have no effect without an <see cref="ItemToggleComponent"/> on the entity.
    /// </summary>
    [DataField]
    public bool RequireActivated = true;

    /// <summary>
    /// An optional required standing state.
    /// Set to true if you need to be standing, false if you need to not be standing, null if you don't care.
    /// </summary>
    [DataField]
    public bool? Standing;

    // Omu start
    /// <summary>
    /// Absolute control of whether the clothing will modify speed.
    /// Set to true if you want to bypass any immunity components. Set to false if you will allow normal modifier function.
    /// </summary>
    [DataField]
    public bool BypassImmune = false;
    // Omu end
}

[Serializable, NetSerializable]
public sealed class ClothingSpeedModifierComponentState : ComponentState
{
    public float WalkModifier;
    public float SprintModifier;

    public ClothingSpeedModifierComponentState(float walkModifier, float sprintModifier)
    {
        WalkModifier = walkModifier;
        SprintModifier = sprintModifier;
    }
}

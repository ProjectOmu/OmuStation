// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class MovementBodyPartComponent : Component
{
    [DataField("walkSpeed")]
    public float WalkSpeed = MovementSpeedModifierComponent.DefaultBaseWalkSpeed;

    [DataField("sprintSpeed")]
    public float SprintSpeed = MovementSpeedModifierComponent.DefaultBaseSprintSpeed;

    [DataField("acceleration")]
    public float Acceleration = MovementSpeedModifierComponent.DefaultAcceleration;

    // Omu start
    /// <summary>
    /// Optional percent shift for walk speed.
    /// </summary>
    [DataField]
    public float? WalkPercent = null;

    [DataField]
    /// <summary>
    /// Optional percent shift for sprint speed.
    /// </summary>
    public float? SprintPercent = null;

    [DataField]
    /// <summary>
    /// Optional percent shift for acceleration.
    /// </summary>
    public float? AccelerationPercent = null;
    // Omu end
}

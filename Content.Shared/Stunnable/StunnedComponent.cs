// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Stunnable;

/// <summary>
/// This is used to temporarily prevent an entity from moving or acting.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedStunSystem))] // Omu, added AutoGenerateComponentState
public sealed partial class StunnedComponent : Component
{ // Omu Start
    [DataField, AutoNetworkedField]
    public float ShakeDecrease = 0.75f;
}; // Omu End

using Content.Shared.Throwing;
using Robust.Shared.GameStates;

namespace Content.Shared._Omu.Weapons.Melee.Components;

/// <summary>
/// This is used for a melee weapon that throws whatever gets hit by it in a line
/// until it hits a wall or a time limit is exhausted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class OmuMeleeThrowOnHitComponent : Component
{

    /// <summary>
    /// What ThrowingUnanchorStrength to set to once emaged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ThrowingUnanchorStrength EmagUnanchorOnHit = ThrowingUnanchorStrength.None;

}

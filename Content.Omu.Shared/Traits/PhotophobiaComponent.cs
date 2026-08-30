using Robust.Shared.Analyzers;
using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Traits;

/// <summary>
///     Used for the Photophobia trait.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PhotophobiaComponent : Component
{
    /// <summary>
    ///     When the player has a flashlight toggled at them, how long should it flash them for?
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FlashDuration = 2f;

    /// <summary>
    ///     When a player has a flashlight toggled at them, should it slow them down? By how much?
    ///     This should be a value between zero and one, where zero is maximum slowdown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FlashSlowdown = 1f;

    /// <summary>
    ///     How much to multiply the strength of the shader's effect by?
    ///     NOTE: This value is clamped between 0 and 1.25, because quite frankly it'd be rude to set this to 10 and flashbang somebody. That's no fun.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ShaderStrengthMultiplier = 1f;
}

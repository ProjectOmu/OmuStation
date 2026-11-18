using Content.Omu.Shared.Clothing.EntitySystems;
using Content.Omu.Shared.Traits;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Clothing.Components;

/// <summary>
///     This component indicates that an item is a Cybernetic Beast's mantle.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CyberneticMantleSystem), typeof(CyberneticBeastSystem))]
public sealed partial class CyberneticMantleComponent : Component, IClothingSlots
{

    /// <summary>
    ///     The mantle will relay an event to these inventory slots when equpped.
    /// </summary>
    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.HEAD;

    /// <summary>
    ///     The minimum "value" (brightness) of the eyes on the visor.
    ///     In effect, if the player's eyes have an HSV value where V is less than this value,
    ///     the value on the visor will be raised be at least this value.
    /// </summary>
    [DataField]
    public float MinEyeColorLevel { get; set; } = 0.5f;
}

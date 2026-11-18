using Content.Omu.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Clothing.Components;

/// <summary>
///     This component indicates that a clothing garment takes up multiple slots.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(ClothingTakesUpExtraSlotsSystem))]
public sealed partial class ClothingTakesUpExtraSlotsComponent : Component
{
    /// <summary>
    /// Inventory slots which the garment should take up, not including it's main slot.
    /// </summary>
    [DataField(required: true)]
    public string[] Slots;
}

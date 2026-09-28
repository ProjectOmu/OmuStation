// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._DV.Carrying;

[RegisterComponent, NetworkedComponent, Access(typeof(CarryingSystem))]
public sealed partial class CarriableComponent : Component
{
    /// <summary>
    /// Number of free hands required
    /// to carry the entity
    /// </summary>
    [DataField]
    public int FreeHandsRequired = 2;

    // imp add
    /// <summary>
    ///     Will override the CarrierOneHand tag for carrier entities, instead using FreeHandsRequired.
    /// </summary>
    [DataField]
    public bool OneHandOverride = false;
}

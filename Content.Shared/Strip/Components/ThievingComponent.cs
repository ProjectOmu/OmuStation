// SPDX-License-Identifier: MIT

using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Popups; // Omustation - Remake EE Traits System - Thieving Trait

namespace Content.Shared.Strip.Components;

/// <summary>
/// Give this to an entity when you want to decrease stripping times
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class ThievingComponent : Component
{
    /// <summary>
    /// How much the strip time should be shortened by
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StripTimeReduction = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    /// Should it notify the user if they're stripping a pocket?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Stealthy;

    /// <summary>
    /// Variable pointing at the Alert modal
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> StealthyAlertProtoId = "Stealthy";

    /// <summary>
    /// Prevent component replication to clients other than the owner,
    /// doesn't affect prediction.
    /// Get mogged.
    /// </summary>
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// (Omustation - Remake EE Traits System - Thieving Trait)
    /// The size of the message which is sent to the person having their items taken from them.
    /// </summary>
    [DataField]
    public PopupType NoiseLevel = PopupType.Large;

    /// <summary>
    /// Omu edit - Thieving trait fixes
    /// Can stealthiness be toggled on and off?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Toggleable = true;
}

/// <summary>
/// Event raised to toggle the thieving component.
/// </summary>
public sealed partial class ToggleThievingEvent : BaseAlertEvent;


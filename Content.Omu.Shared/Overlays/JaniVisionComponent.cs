// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Overlays;
using Content.Shared.Actions;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Overlays;

[RegisterComponent, NetworkedComponent]
public sealed partial class JaniVisionComponent : SwitchableVisionOverlayComponent
{
    public override EntProtoId? ToggleAction { get; set; } = "ToggleJaniVision";

    // The overlay filter color
    public override Color Color { get; set; } = Color.FromHex("#d8bfd8");

    // Highlights all lights or only when something is wrong with them
    [DataField]
    public bool ShowAllLights = false;

    // When the light is either on or off in a working state
    [DataField]
    public Color WorkingLightsColor = Color.White;

    [DataField]
    public Color BrokenLightsColor = Color.Red;

    [DataField]
    public Color EmptyLightsColor = Color.Blue;

    [DataField]
    public Color BurnedLightsColor = Color.Orange;

    [DataField]
    public string? JaniShader = "unshaded";

    [DataField]
    public float DistanceCheckMin = 1;

    [DataField]
    public float DistanceCheckMax = 1.5f;

    [DataField]
    public float DistanceAlpha = 0.2f;

    [DataField]
    public Dictionary<string, Color> TagColors = new()
    {
        { "Trash", Color.Red }
    };
}

public sealed partial class ToggleJaniVisionEvent : InstantActionEvent;

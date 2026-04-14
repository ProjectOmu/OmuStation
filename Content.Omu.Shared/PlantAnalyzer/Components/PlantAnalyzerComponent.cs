using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Omu.Shared.PlantAnalyzer.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PlantAnalyzerComponent : Component
{
    // how long a scan takes (seconds)
    [DataField]
    public float ScanDelay = 2.5f;

    // charge consumed per scan (called twice - advanced always costs 2)
    [DataField]
    public float ScanCharge = 2f;

    [DataField]
    public DoAfterId? DoAfter;

    [DataField]
    public SoundSpecifier? ScanningEndSound;
}

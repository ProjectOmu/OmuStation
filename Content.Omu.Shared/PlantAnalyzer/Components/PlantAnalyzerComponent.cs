using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Omu.Shared.PlantAnalyzer.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PlantAnalyzerComponent : Component
{
    // how long a basic scan takes (seconds)
    [DataField]
    public float BasicScanDelay = 4f;

    // how long an advanced scan takes (seconds)
    [DataField]
    public float AdvancedScanDelay = 8f;

    // charge consumed by a basic scan
    [DataField]
    public float BasicScanCharge = 1f;

    // charge consumed by an advanced scan (checked twice: before and after)
    [DataField]
    public float AdvancedScanCharge = 2f;

    // whether the next scan will be advanced
    [DataField]
    public bool AdvancedMode = false;

    [DataField]
    public DoAfterId? DoAfter;

    [DataField]
    public SoundSpecifier? ScanningEndSound;
}

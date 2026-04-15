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

    // how many charges are consumed per scan
    [DataField]
    public int ScanCharge = 2;

    // how much movement cancels the scan doafter
    [DataField]
    public float MovementThreshold = 0.01f;

    [DataField]
    public DoAfterId? DoAfter;

    [DataField]
    public SoundSpecifier? ScanningEndSound;
}

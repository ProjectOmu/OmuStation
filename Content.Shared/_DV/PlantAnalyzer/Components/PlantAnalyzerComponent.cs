using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._DV.PlantAnalyzer.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PlantAnalyzerComponent : Component
{
    [DataField]
    public float ScanDelay = 2.5f;

    public SoundSpecifier ScanningEndSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");
}

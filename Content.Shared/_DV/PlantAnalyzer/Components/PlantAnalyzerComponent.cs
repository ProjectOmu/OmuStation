using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._DV.PlantAnalyzer.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PlantAnalyzerComponent : Component
{
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(2.5);

    [DataField]
    public SoundSpecifier ScanningEndSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");
}

using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Botany.Components;

[RegisterComponent]
public sealed partial class PlantAnalyzerComponent : Component
{
    [DataField]
    public EntityUid? ScannedEntity;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? OutOfRangeSince = null;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public PlantAnalyzerSettings Settings = new();

    [DataField]
    public SoundSpecifier? ScanningEndSound;
}

[DataRecord]
public partial struct PlantAnalyzerSettings
{
    [DataField]
    public float ScanDelay = 1.5f;

    [DataField]
    public float MaxScanRange = 2f;

    public PlantAnalyzerSettings()
    {
    }
}

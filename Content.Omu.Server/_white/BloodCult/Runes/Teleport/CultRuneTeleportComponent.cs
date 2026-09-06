using Robust.Shared.Audio;

namespace Content.Omu.Server.WhiteDream.BloodCult.Runes.Teleport;

[RegisterComponent]
public sealed partial class CultRuneTeleportComponent : Component
{
    [DataField]
    public float TeleportGatherRange = 0.65f;

    [DataField]
    public string Name = "";

    [DataField]
    public SoundPathSpecifier TeleportInSound = new("/Audio/_White/BloodCult/veilin.ogg");

    [DataField]
    public SoundPathSpecifier TeleportOutSound = new("/Audio/_White/BloodCult/veilout.ogg");
}

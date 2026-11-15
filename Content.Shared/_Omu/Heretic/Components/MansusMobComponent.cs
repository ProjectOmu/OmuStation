using Robust.Shared.Audio;

namespace Content.Shared._Omu.Heretic.Components;

[RegisterComponent]
public sealed partial class MansusMobComponent : Component
{
    [DataField]
    public int ScreechRange = 10;

    [DataField]
    public SoundPathSpecifier ScreechSound = new("/Audio/_Omu/Entities/eerie_scream.ogg");
}

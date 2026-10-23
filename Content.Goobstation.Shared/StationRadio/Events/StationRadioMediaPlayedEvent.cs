using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.StationRadio.Events;

[Serializable, NetSerializable]
public sealed class StationRadioMediaPlayedEvent : EntityEventArgs
{
    public SoundSpecifier MediaPlayed { get; }
    public float Volume { get; }//omu
    public StationRadioMediaPlayedEvent(SoundSpecifier Media, float volume)//omu
    {
        MediaPlayed = Media;
        Volume = volume; //omu
    }
}

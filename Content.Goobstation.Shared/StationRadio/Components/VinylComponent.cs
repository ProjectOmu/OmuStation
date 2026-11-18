using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.StationRadio.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VinylComponent : Component
{
    /// <summary>
    /// What song should be played when the vinyl is played
    /// </summary>
    [DataField] public SoundSpecifier? Song;

    /// <summary>
    /// Sets the volume of the sound track in %
    /// OMU addition
    /// </summary>
    [DataField]
    public float Volume = 1f;
}

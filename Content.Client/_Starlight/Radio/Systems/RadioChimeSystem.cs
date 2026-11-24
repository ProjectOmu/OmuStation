using Content.Shared._Starlight.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Radio.Systems;

/// <summary>
/// This system handles playing radio chime sounds on the client side when radio messages are received.
/// </summary>
public sealed class RadioChimeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _muted;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.RadioChimeMuted, x => _muted = x, true);
    }

    public void PlayChime(SoundSpecifier? chime)
    {
        if (chime == null || _muted)
            return;

        _audio.PlayGlobal(_audio.ResolveSound(chime), Filter.Local(), false, AudioParams.Default.WithVolume(-8f));
    }
}

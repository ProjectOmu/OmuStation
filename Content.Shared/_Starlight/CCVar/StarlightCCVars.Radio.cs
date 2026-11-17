using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Option to mute radio chime sounds
    /// </summary>
    public static readonly CVarDef<bool> RadioChimeMuted =
        CVarDef.Create("audio.radio_chime_muted", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}

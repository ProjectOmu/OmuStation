using Robust.Shared.Configuration;

namespace Content.Omu.Common.CCVar;

[CVarDefs]
public sealed partial class OmuCVars
{
    /// <summary>
    ///     How many seconds a player will have to wait to close the rules smite popup.
    /// </summary>
    public static readonly CVarDef<float> RulesSmiteTime =
        CVarDef.Create("rulessmite.time", 60f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     DEBUG Cvar - Should pathfinding be disabled globally.
    /// </summary>
    public static readonly CVarDef<bool> DisablePathfinding =
        CVarDef.Create("omu.disable_pathfinding", false, CVar.SERVER | CVar.SERVERONLY);

    public static readonly CVarDef<bool> AlternateJobTitles =
        CVarDef.Create("omu.alternate_job_titles", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> DecalPreviewOpacity =
        CVarDef.Create("omu.decal_preview_opacity", 0.75f, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    // Word replacement amount per character, 0 disables feature
    /// </summary>
    public static readonly CVarDef<int> SpeechReplacementsMax =
        CVarDef.Create("omu.speech_replacements_max", 30, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    // How long word/replacement can be
    /// </summary>
    public static readonly CVarDef<int> SpeechReplacementLength =
        CVarDef.Create("omu.speech_replacement_length", 64, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     How muffled sounds are in vacuum, 0 to disable.
    /// </summary>
    public static readonly CVarDef<float> HearingVacuumOcclusion =
        CVarDef.Create("omu.hearing_vacuum_occlusion", 8.5f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Multiplier for how much walls muffle noise, 0 to disable.
    /// </summary>
    public static readonly CVarDef<float> HearingWallOcclusion =
        CVarDef.Create("omu.hearing_wall_occlusion", 1f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Distance between a projectile and a target's lag-compensated position that counts as a hit.
    ///     Lets players with high ping hit what they actually saw when shooting.
    /// </summary>
    public static readonly CVarDef<float> GunLagCompRange =
        CVarDef.Create("omu.gun_lag_comp_range", 0.6f, CVar.SERVER | CVar.SERVERONLY);
}

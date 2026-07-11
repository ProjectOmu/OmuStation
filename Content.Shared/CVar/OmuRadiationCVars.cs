using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> OmuRadiationVisualsEnabled =
        CVarDef.Create("omu.radiation_visuals_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> OmuRadiationSicknessEnabled =
        CVarDef.Create("omu.radiation_sickness_enabled", true, CVar.SERVERONLY);

}

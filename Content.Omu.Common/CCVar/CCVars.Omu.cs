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
}

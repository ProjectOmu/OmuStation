
using Robust.Shared.Configuration;

namespace Content.Shared._Omu.CCvars;

public sealed class OmuCvar
{
    /// <summary>
    ///     Whether antag rolls are weighted by last playtime or not.
    /// </summary>
    public static readonly CVarDef<bool> AntagPity =
        CVarDef.Create("antagpity", false, CVar.SERVERONLY);
}

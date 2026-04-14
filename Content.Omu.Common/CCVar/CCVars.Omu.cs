using System.Runtime.InteropServices.Marshalling;
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

    #region "Server Updater"

    /// <summary>
    ///     Should the server send a request to the updater to restart on round end.
    /// </summary>
    public static readonly CVarDef<bool> ServerUpdaterEnabled =
        CVarDef.Create("updater.server_updater_enabled", false, CVar.SERVER | CVar.SERVERONLY);

    /// <summary>
    ///    The ID of the updater to send the restart request to.
    /// </summary>
    public static readonly CVarDef<string> ServerUpdaterServerId =
        CVarDef.Create("updater.server_id", string.Empty, CVar.SERVER | CVar.SERVERONLY);

    /// <summary>
    ///     The URL of the Pterodactyl panel, used to send the restart request, ex "https://panel.pterodactyl.com/"
    /// </summary>
    public static readonly CVarDef<string> ServerUpdaterPanelUrl =
        CVarDef.Create("updater.panel_url", string.Empty, CVar.SERVER | CVar.SERVERONLY);

    /// <summary>
    ///     The API key to use when sending the restart request, should be in the format "ptlc_YOUR_API_KEY"
    /// </summary>
    public static readonly CVarDef<string> ServerUpdaterApiKey =
        CVarDef.Create("updater.api_key", string.Empty, CVar.SERVER | CVar.SERVERONLY | CVar.CONFIDENTIAL);

    #endregion
}

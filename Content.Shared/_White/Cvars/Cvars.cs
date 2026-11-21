using Robust.Shared.Configuration;

namespace Content.Shared._White;

[CVarDefs]
public sealed class WhiteCVars
{
    #region GhostRespawn
    public static readonly CVarDef<float> GhostRespawnTime = // Omustation - Made this a float. Why was double precision used here?
        CVarDef.Create("ghost.respawn_time", 15f, CVar.SERVERONLY);

    public static readonly CVarDef<int> GhostRespawnMaxPlayers =
        CVarDef.Create("ghost.respawn_max_players", 40, CVar.SERVERONLY);

    #endregion
}
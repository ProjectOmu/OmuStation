// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using Content.Omu.Common.CCVar; // Omu - gun prediction port
using Content.Shared.CCVar;

namespace Content.IntegrationTests;

// Partial class containing test cvars
// This could probably be merged into the main file, but I'm keeping it separate to reduce
// conflicts for forks.
public static partial class PoolManager
{
    public static readonly (string cvar, string value)[] TestCvars =
    {
        // @formatter:off
        (CCVars.DatabaseSynchronous.Name,     "true"),
        (CCVars.DatabaseSqliteDelay.Name,     "0"),
        (CCVars.HolidaysEnabled.Name,         "false"),
        (CCVars.GameMap.Name,                 TestMap),
        (CCVars.AdminLogsQueueSendDelay.Name, "0"),
        (CCVars.NPCMaxUpdates.Name,           "999999"),
        (CCVars.GameRoleTimers.Name,          "false"),
        (CCVars.GameRoleLoadoutTimers.Name,   "false"),
        (CCVars.GameRoleWhitelist.Name,       "false"),
        (CCVars.GridFill.Name,                "false"),
        (CCVars.PreloadGrids.Name,            "false"),
        (CCVars.ArrivalsShuttles.Name,        "false"),
        (CCVars.EmergencyShuttleEnabled.Name, "false"),
        (CCVars.ProcgenPreload.Name,          "false"),
        (CCVars.WorldgenEnabled.Name,         "false"),
        (CCVars.GatewayGeneratorEnabled.Name, "false"),
        (CCVars.GameDummyTicker.Name, "true"),
        (CCVars.GameLobbyEnabled.Name, "false"),
        (CCVars.ConfigPresetDevelopment.Name, "false"),
        (CCVars.AdminLogsEnabled.Name, "false"),
        (CCVars.AutosaveEnabled.Name, "false"),
        (CCVars.InteractionRateLimitCount.Name, "9999999"),
        (CCVars.InteractionRateLimitPeriod.Name, "0.1"),
        (CCVars.MovementMobPushing.Name, "false"),
        (CCVars.LavalandEnabled.Name, "false"), // Lavaland Change
        // Omu - gun prediction port: pin lag compensation to its shipping defaults so that
        // Tests/_Omu/RangedLagCompensationTest.cs does not silently change meaning if the
        // production defaults are ever retuned.
        (OmuCVars.LagCompensationMilliseconds.Name, "750"),
        (OmuCVars.LagCompensationMarginTiles.Name,  "0.25"),
        // Pinned to the full buffer, which effectively disables the ping-plausibility bound for the
        // suite. A loopback pair measures zero ping, so at the shipping default of 150 ms every
        // lag-compensation test would be held to ~4 ticks of rewind and would stop testing what it
        // means to test. The bound itself is covered deliberately by
        // RangedLagCompensationTest.PlausibilityBoundLimitsRewind, which sets this low on purpose.
        (OmuCVars.LagCompensationMinRewindMilliseconds.Name, "750"),
        // Phase 2 (client gun prediction) is pinned OFF for the suite at large, which is also its
        // shipping default. Every pre-existing gun, projectile and combat test therefore exercises
        // exactly the pre-port path, so a failure anywhere in them is a real regression rather than
        // a prediction artefact. Tests/_Omu/GunPredictionTest.cs turns it on per-pair.
        (OmuCVars.GunPrediction.Name, "false"),
        // Omu end
    };
}

// SPDX-FileCopyrightText: 2026 Raze500
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static class EventSchedulerModes
    {
        public const string Legacy = "legacy";
        public const string SecretPlus = "secretplus";
        public const string EventDirector = "event-director";
    }

    /// <summary>
    ///     Enables Omu's event director. when true, BasicStationEventScheduler and
    ///     RampingStationEventScheduler yield so the director is the only active scheduler.
    ///     disabled by default — host enables it via server config.
    /// </summary>
    public static readonly CVarDef<bool>
        EventDirectorEnabled = CVarDef.Create("event_director.enabled", false, CVar.SERVERONLY);

    /// <summary>
    ///     Selects which scheduler owns structured event pacing for the round.
    ///     legacy keeps existing behaviour, secretplus explicitly prefers SecretPlus,
    ///     event-director explicitly prefers Omu Event Director.
    /// </summary>
    public static readonly CVarDef<string>
        EventSchedulerMode = CVarDef.Create("event_scheduler.mode", EventSchedulerModes.Legacy, CVar.SERVERONLY);

    /// <summary>
    ///     Selects which eventDirectorConfig prototype drives the round.
    ///     change this via 'eventdirector setconfig id' in the admin console.
    /// </summary>
    public static readonly CVarDef<string>
        EventDirectorConfig = CVarDef.Create("event_director.config", "OmuDefault", CVar.SERVERONLY);
}

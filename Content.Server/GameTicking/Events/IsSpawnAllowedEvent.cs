// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.Server.GameTicking.Events;

/// <summary>
///     Raised to check whether a player may be spawned at all, independently of which job they would take.
///     Cancel it to refuse the spawn.
/// </summary>
/// <remarks>
///     <para>
///     Use this for gates that refuse the <i>character</i> rather than a <i>job</i> - a character that is not
///     allowed to exist as configured. Job-scoped gates belong on <see cref="IsRoleAllowedEvent"/> or
///     <see cref="GetDisallowedJobsEvent"/>.
///     </para>
///     <para>
///     This exists because no other hook on the spawn path can express a plain refusal:
///     <see cref="Content.Shared.GameTicking.PlayerBeforeSpawnEvent"/> makes the ticker call
///     <see cref="GameTicker.PlayerJoinGame"/> once it is handled, which would drag a refused player out of the
///     lobby with no body; <see cref="IsRoleAllowedEvent"/> is not raised for round-start spawns at all; and
///     <see cref="GetDisallowedJobsEvent"/> is ignored whenever a job has already been picked, which is every
///     round-start spawn.
///     </para>
///     <para>
///     Cancelling produces exactly the same outcome as "no job available": the player stays in the lobby if
///     there is one and becomes an observer if there is not, and <see cref="NoJobsAvailableSpawningEvent"/> is
///     raised so game rules drop any slot they had already handed out.
///     </para>
///     <para>
///     Raised before <see cref="Content.Shared.GameTicking.PlayerBeforeSpawnEvent"/>, so a refusal also beats
///     game rules that spawn the player themselves (deathmatch, wizard, nukies).
///     </para>
/// </remarks>
[ByRefEvent]
public struct IsSpawnAllowedEvent(
    ICommonSession player,
    HumanoidCharacterProfile profile,
    EntityUid station,
    string? jobId,
    bool lateJoin)
{
    public readonly ICommonSession Player = player;
    public readonly HumanoidCharacterProfile Profile = profile;
    public readonly EntityUid Station = station;

    /// <summary>
    ///     The job the player asked for, if any. Null means the ticker has not picked one yet.
    /// </summary>
    public readonly string? JobId = jobId;

    public readonly bool LateJoin = lateJoin;

    public bool Cancelled = false;

    /// <summary>
    ///     Message shown to the refused player. Null shows nothing.
    /// </summary>
    public LocId? Reason = null;

    public void Cancel(LocId reason)
    {
        Cancelled = true;
        Reason = reason;
    }
}

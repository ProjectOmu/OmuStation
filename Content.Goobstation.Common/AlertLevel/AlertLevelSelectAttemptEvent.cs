// SPDX-License-Identifier: AGPL-3.0-or-later
// Omu - moved here from Content.Goobstation.Shared by Omu Station, so upstream Content.Server can raise it without referencing the fork Shared layer.

namespace Content.Goobstation.Common.AlertLevel;

/// <summary>
///     Raised on the station before an alert level is set from a communications console.
///     Lives in the Common tier so upstream server code can raise it without depending on the fork layer.
/// </summary>
[ByRefEvent]
public record struct AlertLevelSelectAttemptEvent(EntityUid Station, EntityUid Console, EntityUid User, string Level)
{
    public bool Cancelled;
}

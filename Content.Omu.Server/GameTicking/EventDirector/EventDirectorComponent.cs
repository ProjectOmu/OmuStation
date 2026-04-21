// SPDX-FileCopyrightText: 2026 Raze500
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Omu.Server.GameTicking.EventDirector;

/// <summary>
///     Attached to the EventDirector game rule entity.
///     Holds all per-round scheduling state so nothing leaks between rounds -
///     the component is fresh every time the game rule starts.
/// </summary>
[RegisterComponent, Access(typeof(EventDirectorSystem), typeof(EventDirectorAdminSystem))]
public sealed partial class EventDirectorComponent : Component
{
    // when the next timer+minor loop fires (absolute round time)
    public TimeSpan? NextLoopFireAt;

    // when the single midround event fires (absolute round time, randomized at roundstart)
    public TimeSpan? NextMidroundRollAt;

    // true once the midround event has already fired this round
    public bool MidroundTriggered;

    // admins can pause the director mid-round without ending the round
    public bool Paused;
}

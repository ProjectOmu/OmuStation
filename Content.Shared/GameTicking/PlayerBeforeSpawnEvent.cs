// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Player;

namespace Content.Shared.GameTicking;

// Omu - documented the Handled contract: the bus keeps dispatching, so every subscriber must check Handled itself.
/// <summary>
///     Event raised broadcast before a player is spawned by the GameTicker.
///     You can use this event to spawn a player off-station on late-join but also at round start.
///     When this event is handled, the GameTicker will not perform its own player-spawning logic.
/// </summary>
/// <remarks>
///     <para>
///     <b>This means "I have spawned them", not "do not spawn them".</b> Setting <see cref="HandledEntityEventArgs.Handled"/>
///     makes the GameTicker call <c>PlayerJoinGame</c> on the player and return, so a subscriber that handles
///     this event without producing a body leaves the player joined to the round with nothing to control, and
///     out of the lobby. To refuse a spawn instead, cancel <c>Content.Server.GameTicking.Events.IsSpawnAllowedEvent</c>,
///     which is raised just before this one.
///     </para>
///     <para>
///     <see cref="HandledEntityEventArgs.Handled"/> is advisory: the event bus never inspects it and does not
///     stop dispatching once it is set. Every subscriber must guard with <c>if (ev.Handled) return;</c> itself.
///     </para>
///     <para>
///     There is also no ordering between subscribers. Nothing in the tree declares <c>before:</c>/<c>after:</c>
///     on this event, so resolution is registration order. If you add a subscriber whose correctness depends on
///     running before or after another one, declare the ordering - and note that doing so switches the whole
///     event onto the ordered dispatch path for every subscriber, not just yours.
///     </para>
/// </remarks>
[PublicAPI]
public sealed class PlayerBeforeSpawnEvent : HandledEntityEventArgs
{
    public ICommonSession Player { get; }
    public HumanoidCharacterProfile Profile { get; }
    public string? JobId { get; }
    public bool LateJoin { get; }
    public EntityUid Station { get; }

    public PlayerBeforeSpawnEvent(ICommonSession player,
        HumanoidCharacterProfile profile,
        string? jobId,
        bool lateJoin,
        EntityUid station)
    {
        Player = player;
        Profile = profile;
        JobId = jobId;
        LateJoin = lateJoin;
        Station = station;
    }
}
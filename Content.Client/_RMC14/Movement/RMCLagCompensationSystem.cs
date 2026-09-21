// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Client/_RMC14/Movement/RMCLagCompensationSystem.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Content.Shared.CombatMode;
using Content.Shared._RMC14.Movement;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Movement;

/// <summary>
///     Client half of lag compensation. The client never rewinds anything; it only knows which
///     server tick it last had authoritative state for, and reports that so the server can rewind
///     targets to what this client was actually looking at.
/// </summary>
/// <remarks>
///     <para>
///     RMC-14 sends this from inside the upstream melee, interaction and shooting paths. Omu sends
///     it from here instead, so that no upstream file has to be touched to make the server half
///     usable. Without a sender the whole subsystem is inert: the server would fall back to
///     "the client is exactly up to date" and never rewind anything at all.
///     </para>
///     <para>
///     It is sent only while the local player is in combat mode, because combat mode is the gate on
///     every attack in the first place (see <c>Content.Client/Weapons/Ranged/Systems/GunSystem.cs</c>
///     and the melee equivalent), and sending it the rest of the time would be pure upstream
///     bandwidth. Going stale is safe, but not for the reason it looks like: the offset the server
///     derives from a stored tick only grows, so "it eventually exceeds the buffer and degrades to
///     no rewind" is not a bound - it hands out every depth up to the buffer on the way there.
///     What makes silence safe is that the server re-applies this
///     session's own allowance when it spends the rewind, in
///     <c>SharedRMCLagCompensationSystem.GetRewindOffset</c>, not only when it stores the claim.
///     </para>
/// </remarks>
public sealed class RMCLagCompensationSystem : SharedRMCLagCompensationSystem
{
    [Dependency] private readonly IClientGameTiming _clientTiming = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    /// <summary>
    ///     Minimum gap, in ticks, between two heartbeats.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The server rewinds by <c>CurTick - LastRealTick</c>, so a value that is N ticks stale makes
    ///     it rewind N ticks too far, which is generous towards the shooter. Throttling therefore has
    ///     to keep N small relative to the 750 ms (<c>omu.lag_compensation_milliseconds</c>) buffer.
    ///     </para>
    ///     <para>
    ///     Between two sends the reported tick can advance by at most <see cref="SendIntervalTicks"/>,
    ///     so that is the staleness bound: 3 ticks, which at the default <c>net.tickrate</c> of 30 is
    ///     100 ms, or 13% of the buffer - about 0.45 tiles of extra rewind for a sprinting target, on
    ///     top of the 0.25 tile <c>omu.lag_compensation_margin_tiles</c> slop that already exists. It
    ///     can never exceed the buffer: <c>SetLastRealTick</c> clamps to <c>MaxRewindTicks</c> and
    ///     <c>LagCompensationSystem</c> degrades to no rewind at all beyond <c>BufferTime</c>.
    ///     </para>
    ///     <para>
    ///     Change detection alone would not help: <see cref="IClientGameTiming.LastRealTick"/> advances
    ///     on essentially every tick for a client that is receiving state, so it would send just as
    ///     often. It is kept as a second gate because it is free and it silences the heartbeat entirely
    ///     while the client is starved of new server state - and in exactly that case re-sending is
    ///     pointless, since the stored value would be identical and therefore not stale at all.
    ///     </para>
    /// </remarks>
    private const uint SendIntervalTicks = 3;

    private EntityQuery<CombatModeComponent> _combatQuery;

    private GameTick _lastSendTick;
    private GameTick _lastSentValue;

    public override void Initialize()
    {
        base.Initialize();

        _combatQuery = GetEntityQuery<CombatModeComponent>();
    }

    public override GameTick GetLastRealTick(NetUserId? session)
    {
        return _clientTiming.LastRealTick;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_clientTiming.IsFirstTimePredicted)
            return;

        // The server has lag compensation turned off entirely; do not spend bandwidth on it.
        if (MaxRewindTicks() == 0)
            return;

        if (_player.LocalEntity is not { } local)
            return;

        if (!_combatQuery.TryComp(local, out var combat) || !combat.IsInCombatMode)
            return;

        // Rate limit. Unsigned wraparound here is harmless: if CurTick ever went backwards (a
        // reconnect resets it) the difference is huge, so we simply send.
        var curTick = _clientTiming.CurTick;
        if (curTick.Value - _lastSendTick.Value < SendIntervalTicks)
            return;

        // Nothing new to tell the server. See the note on SendIntervalTicks.
        var lastReal = _clientTiming.LastRealTick;
        if (lastReal == _lastSentValue)
            return;

        _lastSendTick = curTick;
        _lastSentValue = lastReal;
        SendLastRealTick();
    }
}

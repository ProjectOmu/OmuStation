// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Movement/RMCSetLastRealTickEvent.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Movement;

/// <summary>
///     Out-of-band transport for the last server tick (and physics substep) the client had
///     authoritative state for. The server treats this as an untrusted claim; see
///     <see cref="SharedRMCLagCompensationSystem.SetLastRealTick"/> for the clamp applied to it.
/// </summary>
[Serializable, NetSerializable]
public sealed class RMCSetLastRealTickEvent(GameTick tick, int substep = 0) : EntityEventArgs
{
    public readonly GameTick Tick = tick;
    public readonly int Substep = substep;
}

// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Weapons/Ranged/Prediction/IgnorePredictionHideComponent.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Marks a server-spawned projectile that must stay visible even while the shooter's own
///     client-side copy of it is still in flight.
/// </summary>
/// <remarks>
///     <para>
///     Normally the shooter is shown only its predicted copy, and the authoritative projectile the
///     server sends back is hidden from that one client so the same bullet is not drawn twice.
///     This component suppresses that hiding, for projectiles whose real appearance is load-bearing
///     (visible beams, area effects, anything where the predicted copy is not a faithful stand-in).
///     </para>
///     <para>
///     Networked because the decision to hide is made on the client, which therefore has to be able
///     to see the marker.
///     </para>
/// </remarks>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedGunPredictionSystem))]
public sealed partial class IgnorePredictionHideComponent : Component;

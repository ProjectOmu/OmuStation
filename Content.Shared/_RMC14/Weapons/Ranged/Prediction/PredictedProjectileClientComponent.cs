// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Weapons/Ranged/Prediction/PredictedProjectileClientComponent.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Marks a projectile the shooter's own client spawned speculatively, before the server had
///     confirmed the shot.
/// </summary>
/// <remarks>
///     Client-only: it is deliberately not <c>NetworkedComponent</c>, because these entities exist
///     only in the shooter's world and are reconciled against the authoritative projectile the
///     server later sends back. Lives in <c>Content.Shared</c> so the shared base system can name
///     it, but nothing on the server should ever see one.
/// </remarks>
[RegisterComponent]
public sealed partial class PredictedProjectileClientComponent : Component
{
    /// <summary>
    ///     Whether this predicted copy has already reported a hit to the server.
    /// </summary>
    /// <remarks>
    ///     Latches so one predicted copy only reports a single hit, no matter how many times the
    ///     local physics step brings it into contact with something. This is a local convenience, not
    ///     an enforcement: it lives in the client's own process and a modified client can ignore it.
    ///     The latch that actually makes a projectile single-use is
    ///     <see cref="PredictedProjectileServerComponent.Hit"/>, which the server sets before it
    ///     adjudicates anything.
    /// </remarks>
    [DataField]
    public bool Hit;

    /// <summary>
    ///     Snapshot of where this copy sat before the current physics solve, so the solve can be
    ///     undone during reprediction.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Written for every predicted copy on every tick in the client
    ///     <c>GunPredictionSystem.OnBeforeSolve</c>, and consumed in <c>OnAfterSolve</c>, which
    ///     restores it on every pass except the first-time-predicted one - otherwise repeated
    ///     reprediction of the same tick would advance the copy several ticks' worth of travel. It is
    ///     cleared as soon as it is restored, and it is meaningless outside that one before/after
    ///     solve pair.
    ///     </para>
    ///     <para>
    ///     It has <b>nothing to do with reporting a hit</b>: it is not networked, it is never read by
    ///     the server, and it is never sent anywhere. The position the client claims for a hit is read
    ///     off the target entity at the moment of the predicted collision and travels in
    ///     <see cref="PredictedProjectileHitEvent"/>, not in this field.
    ///     </para>
    /// </remarks>
    [DataField]
    public EntityCoordinates? Coordinates;
}

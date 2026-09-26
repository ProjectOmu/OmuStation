// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Weapons/Ranged/Prediction/PredictedProjectileHitEvent.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Sent by the shooter's client to tell the server what its predicted copy of a projectile hit,
///     and where.
/// </summary>
/// <remarks>
///     <para>
///     Every field is client-supplied and must be treated as a claim, not a fact. The server looks
///     up the projectile by <see cref="Projectile"/> among the ones it spawned <i>for this session</i>,
///     latches it single-use, and then, for each named target, re-derives the collision itself in
///     <c>GunPredictionSystem.Collides</c> against that target's rewound position.
///     </para>
///     <para>
///     Two details the phrase "rewound adjudication" easily hides. The rewind is by the server's own
///     measured channel ping, <i>not</i> by the <c>LastRealTick</c> the client reported in its shoot
///     request - that value feeds the melee and hitscan paths, not this one. And the claimed
///     <see cref="MapCoordinates"/> in <see cref="Hit"/> are never the acceptance criterion: they can
///     only replace the rewound position when they are already within
///     <c>omu.gun_prediction_coordinate_deviation</c> of it (or within the tighter
///     <c>omu.gun_prediction_lowest_coordinate_deviation</c> of the oldest position in the window),
///     and the actual test is whether the <i>server's</i> copy of the projectile lies inside the
///     target's resulting bounds. A claim further out than the tolerance is discarded and the
///     server's own rewound position used instead.
///     </para>
///     <para>
///     Sent per projectile rather than per shot so a burst reconciles shot by shot.
///     </para>
/// </remarks>
/// <param name="projectile">
///     The client's own id for the predicted copy, matching
///     <see cref="PredictedProjectileServerComponent.ClientId"/> on the server projectile it was
///     paired with. Not an entity id.
/// </param>
/// <param name="hit">
///     The entities the copy claims to have struck, each with the map position it claims to have
///     struck them at.
/// </param>
[Serializable, NetSerializable]
public sealed class PredictedProjectileHitEvent(int projectile, HashSet<(NetEntity Id, MapCoordinates Coordinates)> hit) : EntityEventArgs
{
    /// <summary>
    ///     The client's id for the predicted copy that hit. Used to find the paired server
    ///     projectile; untrusted on its own.
    /// </summary>
    public readonly int Projectile = projectile;

    /// <summary>
    ///     What the copy claims to have hit, and where. Each entry is adjudicated separately against
    ///     the rewound position of that entity; the number of entries is deliberately not bounded,
    ///     because each one costs the client a test it has to pass and the projectile is spent either
    ///     way.
    /// </summary>
    public readonly HashSet<(NetEntity Id, MapCoordinates Coordinates)> Hit = hit;
}

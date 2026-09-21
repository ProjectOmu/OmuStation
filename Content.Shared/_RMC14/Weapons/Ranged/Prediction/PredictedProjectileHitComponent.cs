// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Weapons/Ranged/Prediction/PredictedProjectileHitComponent.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Content.Shared.Projectiles;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Tags a projectile whose predicted hit has <i>already been accepted</i>, and records how much
///     further it should visibly fly before it is retired.
/// </summary>
/// <remarks>
///     <para>
///     <b>This is not a security bound, and it does not bound anything a client claims.</b> Nothing
///     reads <see cref="Origin"/> or <see cref="Distance"/> to decide whether a hit is legitimate.
///     The component is only ever <i>written</i>, after a hit has already been adjudicated and
///     applied, by <c>Content.Server.Projectiles.ProjectileSystem.ProjectileCollide</c> at the
///     disposal branch that would otherwise have deleted the projectile outright. Its only two
///     readers tree-wide are the <c>Update</c> loops of the server and client
///     <c>_RMC14.Weapons.Ranged.Prediction.GunPredictionSystem</c>. The checks that actually bound a
///     claimed hit all live in the server <c>GunPredictionSystem</c> - the session-scoped id lookup,
///     the single-use <c>Hit</c> latch, and the rewound re-derivation in <c>Collides</c> - and none
///     of them consults this component. In particular, <b>nothing here limits how far from the
///     muzzle a client may claim a hit</b>; that is bounded only by where the server's own copy of
///     the projectile has actually reached when <c>Collides</c> runs.
///     </para>
///     <para>
///     What it is really for: a predicted hit is adjudicated against a <i>rewound</i> target
///     position, which sits behind where the shooter's client has already drawn the bullet. Deleting
///     the projectile at that moment would make it vanish in mid-air, short of the thing it just hit.
///     So the server keeps the projectile instead and records where it was when the hit landed
///     (<see cref="Origin"/>) and how much further it still has to go (<see cref="Distance"/>). The
///     server's <c>Update</c> deletes it once it has covered that distance; the client's <c>Update</c>
///     reads the same two numbers and hides the sprite at the same point. A deferred-despawn and
///     sprite-hide bound, nothing more.
///     </para>
///     <para>
///     Networked so both halves work from the same numbers and retire the bullet in the same place.
///     <see cref="SharedProjectileSystem"/> is a friend because the concrete server
///     <c>ProjectileSystem</c> - which writes both fields - derives from it; that type is
///     <c>Content.Shared.Projectiles.SharedProjectileSystem</c> and is reachable here.
///     </para>
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGunPredictionSystem), typeof(SharedProjectileSystem))]
public sealed partial class PredictedProjectileHitComponent : Component
{
    /// <summary>
    ///     Where <i>this projectile</i> was at the moment its predicted hit was accepted, as mover
    ///     coordinates. Not the muzzle of the gun and not the shot's origin: it is the start of the
    ///     short stretch the bullet is still allowed to fly for appearance's sake.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityCoordinates Origin;

    /// <summary>
    ///     How far past <see cref="Origin"/> the projectile should keep travelling before it is
    ///     retired, in tiles: the distance from <see cref="Origin"/> to the target's live position at
    ///     the moment the hit was accepted. Reaching it makes the server delete the projectile and the
    ///     client hide its sprite. Nothing is rejected on the strength of this number.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Distance;
}

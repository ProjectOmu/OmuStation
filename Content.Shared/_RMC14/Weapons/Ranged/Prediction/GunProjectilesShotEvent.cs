// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Raised after a player-requested shot has spawned its projectiles, so the prediction layer can
///     pair each server projectile with the client-side copy the shooter already drew for it.
/// </summary>
/// <remarks>
///     <para>
///     This exists instead of a second <c>RequestShootEvent</c> subscriber. <c>SharedGunSystem</c>
///     already owns that event and applies four fork-specific rules to it (multishot, mech pilots,
///     held-item blocking and Goobstation burst target locking); a prediction system that subscribed
///     alongside it would fire the gun a second time, because the bus does not stop dispatching after
///     the first subscriber. Raising this afterwards keeps one entry point and one shot.
///     </para>
///     <para>
///     Nothing subscribes on the client - the client already knows which copies it spawned.
///     </para>
/// </remarks>
/// <param name="Gun">The gun that fired.</param>
/// <param name="Spawned">The projectiles the server just spawned, in spawn order.</param>
/// <param name="PredictedIds">
///     The client-side ids the shooter reported, in the order it spawned them, or null if the shooter
///     is not predicting. Untrusted, and not necessarily the same length as <paramref name="Spawned"/>
///     - the server may fire a different number of shots than the client guessed.
/// </param>
/// <param name="Shooter">The session that asked for the shot.</param>
[ByRefEvent]
public readonly record struct GunProjectilesShotEvent(
    Entity<GunComponent> Gun,
    List<EntityUid> Spawned,
    List<int>? PredictedIds,
    ICommonSession Shooter);

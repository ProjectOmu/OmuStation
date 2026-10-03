// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Weapons/Ranged/Prediction/GunIgnorePredictionComponent.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Opt-out marker for a gun whose projectiles the server must not pair with client-side predicted
///     copies.
/// </summary>
/// <remarks>
///     <para>
///     Intended for guns whose projectiles the client cannot faithfully reproduce ahead of the server
///     - anything whose spawn depends on server-only state, randomness, or a projectile the client
///     would draw in the wrong place.
///     </para>
///     <para>
///     <b>What it actually does.</b> This component is consulted in exactly one place tree-wide:
///     <c>Content.Server._RMC14.Weapons.Ranged.Prediction.GunPredictionSystem.OnGunProjectilesShot</c>
///     returns early for a gun carrying it, so none of that shot's projectiles is given a
///     <see cref="PredictedProjectileServerComponent"/>. Server-side that is complete: an unpaired
///     projectile is a plain server projectile, so no hit report can resolve to it and the shooter's
///     client never hides its sprite.
///     </para>
///     <para>
///     // TODO Omu gun prediction: the client half of this opt-out does not exist, and its absence
///     makes things worse rather than better. <c>PredictShot</c> and <c>ShootPredicted</c> in
///     <c>Content.Client.Weapons.Ranged.Systems.GunSystem</c> never check this component, so a
///     predicting client still spawns its client-side copies for such a gun and still sends their ids
///     in the shoot request - contrary to what this file previously claimed. Because the server then
///     refuses to pair them, no <see cref="PredictedProjectileServerComponent"/> is ever created for
///     that shot, so the client's <c>OnServerProjectileRemove</c> - the only thing that retires a
///     copy - never fires, and <c>RetirePredictedProjectileCopy</c> is never reached. The copy is left
///     on screen alongside the real server projectile until the prototype's own
///     <c>TimedDespawn</c> expires (about ten seconds for a typical bullet), or indefinitely if the
///     projectile has no <c>TimedDespawn</c>: a lingering ghost bullet. Until <c>PredictShot</c> and
///     <c>ShootPredicted</c> are taught to skip a gun with this component, marking a gun with it
///     increases the shooter's visual desync instead of removing it.
///     </para>
///     <para>
///     Networked so that the client can see the marker on a gun it does not own the server state of -
///     which is precisely what the missing client-side check would need.
///     </para>
/// </remarks>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedGunPredictionSystem))]
public sealed partial class GunIgnorePredictionComponent : Component;

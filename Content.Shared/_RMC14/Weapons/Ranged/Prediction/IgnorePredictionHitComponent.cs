// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Weapons/Ranged/Prediction/IgnorePredictionHitComponent.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Marks an entity whose collisions an honest client's predicted copy will not report.
/// </summary>
/// <remarks>
///     <para>
///     Put this on things a predicted projectile may pass through or strike without the shooter's
///     copy reacting: the client skips such an entity in both of its hit paths
///     (<c>OnClientProjectileStartCollide</c> and the contact sweep in <c>Update</c>), so it neither
///     reports nor consumes the collision and the shooter simply waits for the server's own.
///     </para>
///     <para>
///     <b>Enforced on both sides.</b> An honest client filters such an entity out of its own hit
///     paths, and the server's <c>GunPredictionSystem.ProcessPredictedHit</c> independently refuses
///     any claim naming one, so a modified client that reports it anyway gains nothing.
///     </para>
///     <para>
///     The server-side check is an Omu addition; the source relies on the client filter alone, which
///     makes the marker advisory rather than binding. It is listed among the untrusted-input rules in
///     that method for the same reason as the others: every client-supplied field in a hit report has
///     to be re-derived server-side, because nothing about the report is validated by the engine.
///     </para>
///     <para>
///     <b>Deviation from the source (C):</b> RMC-14 declares
///     <c>[Access(typeof(SharedGunPredictionSystem), typeof(LineSystem))]</c> and imports
///     <c>Content.Shared._RMC14.Line</c>, because its line-drawing system marks the entities it
///     spawns along a line. Omu has no <c>LineSystem</c> and no <c>_RMC14.Line</c> namespace, so
///     that friend and its using are dropped; the attribute would not compile otherwise. Re-add
///     the friend if a line system is ever ported.
///     </para>
/// </remarks>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedGunPredictionSystem))]
public sealed partial class IgnorePredictionHitComponent : Component;

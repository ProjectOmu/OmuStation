// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Weapons/Ranged/Prediction/PredictedProjectileServerComponent.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Robust.Shared.GameStates;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     The server's half of a predicted shot: ties an authoritative projectile back to the
///     client-side copy the shooter already drew for it, and to the session that asked for it.
/// </summary>
/// <remarks>
///     The pairing is what lets the shooter be shown one bullet instead of two, and lets a hit the
///     client claims be matched against the projectile the server actually spawned.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PredictedProjectileServerComponent : Component
{
    /// <summary>
    ///     The session that fired this projectile, and therefore the only client whose predicted
    ///     copy may be reconciled against it.
    /// </summary>
    /// <remarks>
    ///     Server-side only and not a <c>DataField</c>: a session is not serialisable and means
    ///     nothing on a client or in a saved map.
    /// </remarks>
    public ICommonSession? Shooter;

    /// <summary>
    ///     The shooter's own id for its predicted copy of this projectile, as reported in the
    ///     shoot request.
    /// </summary>
    /// <remarks>
    ///     A client-chosen number, not an entity id, and therefore untrusted: it is only ever used
    ///     to look up a copy belonging to <see cref="Shooter"/>.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public int ClientId;

    /// <summary>
    ///     The shooter's <i>body</i> - <c>Shooter.AttachedEntity</c> at the time of the shot - not the
    ///     predicted copy.
    /// </summary>
    /// <remarks>
    ///     Networked, and set on the server in <c>GunPredictionSystem.OnGunProjectilesShot</c>; it is
    ///     populated on both sides, not null on the server. Its only use is on the client, which
    ///     compares it against <c>LocalEntity</c> to tell "my bullet" from someone else's before
    ///     hiding a sprite or retiring a copy. The predicted copy itself is found from
    ///     <see cref="ClientId"/>, which the client reinterprets as a local <c>EntityUid</c>.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public EntityUid? ClientEnt;

    /// <summary>
    ///     Whether this projectile has already had a hit adjudicated, so a second claim for the
    ///     same projectile cannot be honoured.
    /// </summary>
    [DataField]
    public bool Hit;
}

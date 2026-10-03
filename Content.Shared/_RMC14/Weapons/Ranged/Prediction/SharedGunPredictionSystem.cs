// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/Weapons/Ranged/Prediction/SharedGunPredictionSystem.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Content.Omu.Common.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Shared base for client-side gun prediction: holds the master on/off switch and the map
///     sanity checks that both the client and server halves need.
/// </summary>
/// <remarks>
///     <para>
///     <b>Deviation from the source (A): this class does not own the shoot request.</b> RMC-14's
///     version carries a <c>ShootRequested</c> method that validates a <c>RequestShootEvent</c> and
///     calls <c>AttemptShoot</c> itself, with its subclasses subscribing to that event. Omu's
///     <see cref="Content.Shared.Weapons.Ranged.Systems.SharedGunSystem"/> already subscribes to
///     <c>RequestShootEvent</c> via <c>SubscribeAllEvent</c> and applies four fork-specific rules
///     inside that handler - the Goobstation multishot skip, the mech-pilot user redirect, the
///     held-item (carried felinid) block, and the Goobstation burst target lock. Subscribing a
///     second time would fire the gun twice, because the event bus keeps dispatching to every
///     subscriber rather than stopping at the first, and those four rules would be bypassed by the
///     copy that ran here. Prediction therefore hooks the existing single entry point instead:
///     <c>SharedGunSystem.OnShootRequestReceived</c> for the reported tick, and
///     <see cref="GunProjectilesShotEvent"/> after the projectiles are spawned for the pairing.
///     </para>
///     <para>
///     <b>Deviation from the source (B):</b> the source's <c>ShootRequested</c> also redirected a
///     click on a vehicle to its rider through <c>VehicleRideSurfaceSystem</c>. Omu has no such
///     system; since the only caller was <c>ShootRequested</c>, the dependency is simply gone
///     rather than stubbed. Nothing here needs it.
///     </para>
///     <para>
///     What is left is deliberately small: a CVar-backed flag and the map checks, both of which are
///     used by the client and server subclasses.
///     </para>
/// </remarks>
public abstract partial class SharedGunPredictionSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;

    /// <summary>
    ///     Whether client-side gun prediction is enabled, mirroring
    ///     <see cref="OmuCVars.GunPrediction"/>.
    /// </summary>
    /// <remarks>
    ///     Replicated, so client and server agree. When false the client spawns no predicted copies
    ///     and sends no predicted ids, and the server adjudicates every shot exactly as it did
    ///     before this port. Note the CVar defaults to <c>false</c> on Omu, unlike the source.
    /// </remarks>
    public bool GunPrediction { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, OmuCVars.GunPrediction, v => GunPrediction = v, true);
    }

    /// <summary>
    ///     Whether two entities are on the same map. Used to drop a cross-map collision, which a
    ///     stale or hostile client can otherwise ask for.
    /// </summary>
    /// <remarks>
    ///     No shot is ever rejected on the strength of this: the two callers are the client's own hit
    ///     paths, which decline to report such a contact, and - in its
    ///     <see cref="MapCoordinates"/> overloads - the server's <c>Collides</c>, which is where the
    ///     check is actually enforced against an incoming claim.
    /// </remarks>
    /// <returns>True only if both entities have a real (non-nullspace) map and it is the same one.</returns>
    protected bool IsSameMap(EntityUid entity, EntityUid other)
    {
        return TryGetMapId(entity, out var mapId) &&
               TryGetMapId(other, out var otherMapId) &&
               mapId == otherMapId;
    }

    /// <summary>
    ///     Whether an entity sits on the same map as a set of map coordinates.
    /// </summary>
    /// <returns>
    ///     True only if the coordinates are not in nullspace and the entity has that same map.
    /// </returns>
    protected bool IsSameMap(EntityUid entity, MapCoordinates coordinates)
    {
        return coordinates.MapId != MapId.Nullspace &&
               TryGetMapId(entity, out var mapId) &&
               mapId == coordinates.MapId;
    }

    /// <summary>
    ///     Whether two sets of map coordinates are on the same map.
    /// </summary>
    /// <returns>True only if both share a real (non-nullspace) map.</returns>
    protected bool IsSameMap(MapCoordinates coordinates, MapCoordinates other)
    {
        return coordinates.MapId != MapId.Nullspace &&
               coordinates.MapId == other.MapId;
    }

    /// <summary>
    ///     Resolves the map an entity is on, treating nullspace and a missing transform alike as
    ///     "no map", so callers never compare two nullspace ids and call them equal.
    /// </summary>
    /// <param name="entity">The entity to locate.</param>
    /// <param name="mapId">The entity's map, or <see cref="MapId.Nullspace"/> on failure.</param>
    /// <returns>True if the entity is on a real map.</returns>
    private bool TryGetMapId(EntityUid entity, out MapId mapId)
    {
        mapId = MapId.Nullspace;

        if (!TryComp(entity, out TransformComponent? xform))
            return false;

        mapId = xform.MapID;
        return mapId != MapId.Nullspace;
    }
}

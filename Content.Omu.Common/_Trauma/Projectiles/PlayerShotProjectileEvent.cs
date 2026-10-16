// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Omu.Common._Trauma.Projectiles;

/// <summary>
/// Event broadcast when a projectile is shot with a non-null user.
/// </summary>
[ByRefEvent]
public record struct PlayerShotProjectileEvent(EntityUid Projectile, EntityUid User);

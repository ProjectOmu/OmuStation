// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Shared._Trauma.Projectiles;

/// <summary>
/// Event broadcast before deleting a projectile that has hit something.
/// </summary>
[ByRefEvent]
public record struct DeletingProjectileEvent(EntityUid Entity);

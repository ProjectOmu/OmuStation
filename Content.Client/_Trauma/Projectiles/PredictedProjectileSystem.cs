// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Projectiles;
using Content.Shared._Trauma.Projectiles;
using Content.Omu.Common._Trauma.Projectiles;
using Robust.Client.GameObjects;
using Robust.Client.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Spawners;

namespace Content.Client._Trauma.Projectiles;

/// <summary>
/// Hides the server-spawned projectile when firing a predicted gun.
/// </summary>
public sealed class PredictedProjectileSystem : EntitySystem
{
    [Dependency] private readonly Robust.Client.Physics.PhysicsSystem _physics = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
        SubscribeNetworkEvent<ShotPredictedProjectileEvent>(OnShotPredictedProjectile);
    }

    private void OnUpdateIsPredicted(Entity<ProjectileComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;

        if (HasComp<PredictedSpawnComponent>(ent.Owner))
            RemComp<TimedDespawnComponent>(ent.Owner);
    }

    private void OnShotPredictedProjectile(ShotPredictedProjectileEvent args)
    {
        var uid = GetEntity(args.Projectile);
        if (!uid.IsValid())
            return;

        _physics.UpdateIsPredicted(uid);
        // TODO: come up with solution to fix the jitter when clientside entity is deleted and serverside one is spawned back at the shooter
        // clientside components have no way to persist so this may need engine work
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Omu.Client.Weapons.Ranged;

public sealed class MuzzleSmokeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MuzzleSmokeComponent, GunMuzzleFlashAttemptEvent>(OnPredictedFlash);
        SubscribeNetworkEvent<MuzzleFlashEvent>(OnNetworkFlash);
    }

    private void OnPredictedFlash(Entity<MuzzleSmokeComponent> ent, ref GunMuzzleFlashAttemptEvent args)
    {
        if (args.Cancelled
            || !_timing.IsFirstTimePredicted
            || !TryComp(ent, out GunComponent? gun)
            || gun.ShootCoordinates is not { } target)
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var direction = _transform.ToMapCoordinates(target).Position - origin.Position;
        SpawnSmoke(ent, origin, direction.ToAngle());
    }

    private void OnNetworkFlash(MuzzleFlashEvent args)
    {
        var gun = GetEntity(args.Uid);
        if (TryComp(gun, out MuzzleSmokeComponent? smoke))
            SpawnSmoke((gun, smoke), _transform.GetMapCoordinates(gun), args.Angle);
    }

    private void SpawnSmoke(Entity<MuzzleSmokeComponent> ent, MapCoordinates origin, Angle angle)
    {
        if (origin.MapId == MapId.Nullspace)
            return;

        Spawn(ent.Comp.Effect, origin.Offset(angle.ToVec() * 0.5f));
    }
}

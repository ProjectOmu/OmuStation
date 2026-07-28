using Content.Omu.Common.Shuttles.Systems;
using Content.Server.Shuttles.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Omu.Server.Shuttles.Systems;

public sealed class OmuShuttleSystemGridFill : CommonOmuShuttleSystemGridFill
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    // Im literally just doing what dungeon spawning does here but it looks nicer and i dont have to paste all this shit in core.

    public override void OmuGridSpawnNearMinimums(Entity<MapGridComponent> grid, EntityUid targetGrid, float minimums, float maximums)
    {
        var targetPhysics = _physicsQuery.Comp(targetGrid);
        var xformGrid = _xformQuery.Comp(grid);
        var spawnCoords = new EntityCoordinates(targetGrid, targetPhysics.LocalCenter);

        var distancePadding = MathF.Max(grid.Comp.LocalAABB.Width, grid.Comp.LocalAABB.Height);

        spawnCoords = spawnCoords.Offset(_random.NextVector2(distancePadding + minimums, distancePadding + maximums));

        _shuttle.TryFTLProximity((grid.Owner, xformGrid), spawnCoords);
    }
}

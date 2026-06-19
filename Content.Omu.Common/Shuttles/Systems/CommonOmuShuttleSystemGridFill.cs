using Robust.Shared.Map.Components;

namespace Content.Omu.Common.Shuttles.Systems;

public abstract class CommonOmuShuttleSystemGridFill : EntitySystem
{
    public abstract void OmuGridSpawnNearMinimums(Entity<MapGridComponent> grid, EntityUid targetGrid, float minimums, float maximums);
}

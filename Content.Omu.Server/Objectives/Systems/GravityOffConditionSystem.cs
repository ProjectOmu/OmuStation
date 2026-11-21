// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Components;
using Content.Server.Station.Systems;
using Content.Shared.Gravity;
using Content.Shared.Objectives.Components;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Objectives.Systems;

public sealed class GravityOffConditionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GravityChangedEvent>(OnGravityChanged);
        SubscribeLocalEvent<GravityOffConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<GravityOffConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnAssigned(Entity<GravityOffConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (_station.GetOwningStation(args.Mind.OwnedEntity) is not { } station
            || _station.GetLargestGrid(station) is not { } grid
            || !HasGenerator(grid))
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Grid = grid;
    }

    private void OnGravityChanged(ref GravityChangedEvent args)
    {
        using var query = EntityQueryEnumerator<GravityOffConditionComponent>();
        while (query.MoveNext(out var condition))
        {
            if (condition.Grid != args.ChangedGridIndex)
                continue;

            if (!args.HasGravity)
            {
                condition.LostAt ??= _timing.CurTime;
                continue;
            }

            condition.Longest = GetLongest(condition);
            condition.LostAt = null;
        }
    }

    private void OnGetProgress(Entity<GravityOffConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = Math.Min(1f, (float) (GetLongest(ent.Comp) / ent.Comp.Duration));
    }

    private TimeSpan GetLongest(GravityOffConditionComponent condition)
    {
        return condition.LostAt is { } lostAt && _timing.CurTime - lostAt > condition.Longest
            ? _timing.CurTime - lostAt
            : condition.Longest;
    }

    private bool HasGenerator(EntityUid grid)
    {
        using var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var xform))
        {
            if (xform.GridUid == grid)
                return true;
        }

        return false;
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Server._Starlight.Shuttles.Systems;
using Content.Server._Starlight.Shuttles.Components;
using Content.Server.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

public sealed class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!; // _Starlight
    [Dependency] private RadarLaserSystem _laserSystem = default!; // _Starlight

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarConsoleComponent, ComponentStartup>(OnRadarStartup);
    }

    private void OnRadarStartup(EntityUid uid, RadarConsoleComponent component, ComponentStartup args)
    {
        UpdateState(uid, component);
    }

    protected override void UpdateState(EntityUid uid, RadarConsoleComponent component)
    {
        var xform = Transform(uid);
        var onGrid = xform.ParentUid == xform.GridUid;
        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;
        Angle? angle = onGrid ? xform.LocalRotation : null;

        if (component.FollowEntity)
        {
            coordinates = new EntityCoordinates(uid, Vector2.Zero);
            angle = Angle.Zero;
        }

        if (_uiSystem.HasUi(uid, RadarConsoleUiKey.Key))
        {
            NavInterfaceState state;
            var docks = _console.GetAllDocks();

            if (coordinates != null && angle != null)
            {
                state = _console.GetNavState(uid, docks, coordinates.Value, angle.Value);
            }
            else
            {
                state = _console.GetNavState(uid, docks);
            }

            state.RotateWithEntity = !component.FollowEntity;

            // Starlight start - populate laser traces
            var consoleMapCoords = _transformSystem.GetMapCoordinates(uid);
            var maxRangeSq = state.MaxRange * state.MaxRange;
            // Populate laser traces from hitscan guns with RadarLaserTrackerComponent.
            var laserQuery = AllEntityQuery<RadarLaserTrackerComponent, TransformComponent>();
            while (laserQuery.MoveNext(out var laserUid, out var tracker, out var laserXform))
            {
                if (laserXform.MapID != consoleMapCoords.MapId)
                    continue;
                foreach (var (origin, dir, _) in tracker.Traces)
                {
                    // Only show traces from guns within radar range.
                    if ((origin.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                        continue;
                    state.Lasers.Add(new RadarLaserData(
                        GetNetCoordinates(laserXform.Coordinates),
                        dir,
                        tracker.MaxRange,
                        tracker.LaserColor));
                }
            }
            // Starlight end

            _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, new NavBoundUserInterfaceState(state));
        }
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Omu.Common.CCVar;
using Content.Omu.Shared.Audio;
using Content.Shared.Audio;
using Content.Shared.Doors.Components;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Omu.Client.Audio;

public sealed class VacuumHearingSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly Vector2[] Neighbors = [new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f)];

    private const int OcclusionMask = (int) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable);
    private const float WallWeight = 2.5f;
    private const float DoorWeight = 1.8f;
    private const float WindowWeight = 1.5f;
    private const float FlimsyWeight = 0.25f;
    private const float ContactRange = 1.5f;
    private const float SourceClearance = 0.75f;
    private const float MuffleStartPressure = 60f;
    private const float OwnOcclusion = 2.7f;
    private const float StructureOcclusion = 4.5f;
    private const float StructureOcclusionPerTile = 0.4f;
    private const float VacuumWallFactor = 0.5f;
    private const float OwnBodyRange = 0.3f;
    private const float OwnActionRange = 3f;
    private const float ParentMatchRange = 0.5f;
    private const float SourceMatchRange = 0.05f;
    private const int OwnSearchDepth = 4;

    private readonly record struct Placement(bool OpenSpace, EntityUid? Grid);

    private readonly record struct SourceInfo(
        MapCoordinates Position,
        EntityUid Parent,
        Placement Placement,
        bool GridWide,
        bool Own,
        bool Predicted);

    private readonly record struct Muffler(
        MapId Map,
        Vector2 Position,
        Angle InverseRotation,
        Box2 Bounds,
        float Weight);

    private EntityQuery<AmbientSoundComponent> _ambientQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<OccluderComponent> _occluderQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private float _maxRayLength;
    private float _vacuumOcclusion;
    private float _wallOcclusion;
    private float _snapshotInterval;
    private float _snapshotRemaining;
    private float _listenerMuffle;
    private EntityUid? _listener;
    private EntityUid? _listenerGrid;
    private bool _listenerGrounded;
    private List<SourceInfo> _sources = new();
    private List<SourceInfo> _sourcesNext = new();
    private List<Muffler> _mufflers = new();
    private List<Muffler> _mufflersNext = new();

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(AudioSystem));
        _ambientQuery = GetEntityQuery<AmbientSoundComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _occluderQuery = GetEntityQuery<OccluderComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_cfg, CVars.AudioRaycastLength, value => _maxRayLength = value, true);
        Subs.CVar(_cfg, CVars.AudioTickRate, OnAudioTickRate, true);
        Subs.CVar(_cfg, OmuCVars.HearingVacuumOcclusion, value => _vacuumOcclusion = value, true);
        Subs.CVar(_cfg, OmuCVars.HearingWallOcclusion, value => _wallOcclusion = value, true);
        _audio.GetOcclusionOverride += GetOcclusion;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _audio.GetOcclusionOverride -= GetOcclusion;
    }

    private void OnAudioTickRate(int value)
    {
        _snapshotInterval = 1f / value;
        _snapshotRemaining = MathF.Min(_snapshotRemaining, _snapshotInterval);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _snapshotRemaining -= frameTime;
        if (_snapshotRemaining > 0f)
            return;

        _snapshotRemaining = MathF.Max(0f, _snapshotInterval + _snapshotRemaining);

        _listener = _player.LocalEntity;
        _listenerMuffle = TryComp(_listener, out HearingPressureComponent? hearing)
            ? Math.Clamp(1f - hearing.Pressure / MuffleStartPressure, 0f, 1f)
            : 0f;

        _listenerGrid = _listener != null ? Transform(_listener.Value).GridUid : null;
        _listenerGrounded = _listenerGrid != null && !_gravity.IsWeightless(_listener!.Value);

        _sourcesNext.Clear();

        using var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var xform))
        {
            if (audio.Global || xform.MapID == MapId.Nullspace || (audio.Flags & AudioFlags.NoOcclusion) != 0)
                continue;

            var parent = xform.ParentUid;
            var gridWide = (audio.Flags & AudioFlags.GridAudio) != 0;
            var position = new MapCoordinates(
                gridWide ? _maps.GetGridPosition(parent) : _transform.GetWorldPosition(xform),
                xform.MapID);

            var placement = GetPlacement(parent, position);
            _sourcesNext.Add(Describe(parent, position, placement, gridWide, IsClientSide(uid)));
        }

        (_sources, _sourcesNext) = (_sourcesNext, _sources);

        _mufflersNext.Clear();

        using var mufflers = AllEntityQuery<SoundMufflerComponent, TransformComponent>();
        while (mufflers.MoveNext(out var uid, out var muffler, out var xform))
        {
            if (xform.MapID == MapId.Nullspace || GetLocalBounds(uid) is not { } bounds)
                continue;

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            _mufflersNext.Add(new Muffler(xform.MapID, position, -rotation, bounds, muffler.Weight));
        }

        (_mufflers, _mufflersNext) = (_mufflersNext, _mufflers);
    }

    private Placement GetPlacement(EntityUid parent, MapCoordinates position)
    {
        if (_gridQuery.HasComp(parent))
            return new Placement(false, parent);

        var placement = GetOpenSpace(position);
        if (!placement.OpenSpace || !_mapQuery.HasComp(parent))
            return placement;

        foreach (var offset in Neighbors)
        {
            var beside = GetOpenSpace(new MapCoordinates(position.Position + offset, position.MapId));
            if (!beside.OpenSpace)
                return beside;
        }

        return placement;
    }

    private SourceInfo Describe(
        EntityUid parent,
        MapCoordinates position,
        Placement placement,
        bool gridWide,
        bool clientSide)
    {
        var own = _listener is { } listener && IsOwnedBy(parent, listener);
        var predicted = !own
            && _listener != null
            && clientSide
            && !_ambientQuery.HasComp(parent)
            && !_mobQuery.HasComp(parent);

        return new SourceInfo(position, parent, placement, gridWide, own, predicted);
    }

    private float GetOcclusion(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt)
    {
        var position = new MapCoordinates(listener.Position + delta, listener.MapId);
        var source = Nearest(position, ignoredEnt, ParentMatchRange)
            ?? Nearest(position, null, SourceMatchRange);

        if (source == null && ignoredEnt is { } parent)
            source = Describe(parent, position, GetOpenSpace(position), false, true);

        var own = distance < OwnBodyRange
            || source is { Own: true }
            || source is { Predicted: true } && distance <= OwnActionRange;
        var vacuum = MathF.Max(_listenerMuffle, source is { Placement.OpenSpace: true } ? 1f : 0f);

        var walls = 0f;
        if (!own && source is not { GridWide: true } && distance > ContactRange && _wallOcclusion > 0f)
        {
            walls = GetWallOcclusion(listener, delta, distance, ignoredEnt)
                * _wallOcclusion
                * (1f - vacuum * (1f - VacuumWallFactor));
        }

        var vacuumOcclusion = _vacuumOcclusion;
        if (own)
        {
            vacuumOcclusion = MathF.Min(vacuumOcclusion, OwnOcclusion);
        }
        else if (_listenerGrounded
            && source is { Placement: { OpenSpace: false, Grid: { } grid } }
            && grid == _listenerGrid)
        {
            vacuumOcclusion = MathF.Min(vacuumOcclusion, StructureOcclusion + StructureOcclusionPerTile * distance);
        }

        return walls + vacuumOcclusion * vacuum;
    }

    private float GetWallOcclusion(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEnt)
    {
        var rayLength = MathF.Min(distance - SourceClearance, _maxRayLength);
        if (rayLength <= 0f)
            return 0f;

        var direction = delta / distance;
        var ray = new CollisionRay(listener.Position, direction, OcclusionMask);
        var hits = _physics.IntersectRayWithPredicate(listener.MapId,
            ray,
            ignoredEnt,
            static (uid, ignored) => uid == ignored,
            rayLength,
            returnOnFirstHit: false);

        var occlusion = 0f;
        var counted = new List<EntityUid>();
        foreach (var hit in hits)
        {
            if (counted.Contains(hit.HitEntity))
                continue;

            counted.Add(hit.HitEntity);
            occlusion += ObstacleWeight(hit.HitEntity);
        }

        var start = listener.Position + direction * SourceClearance;
        foreach (var muffler in _mufflers)
        {
            if (muffler.Map == listener.MapId && Crosses(muffler, start, direction, rayLength - SourceClearance))
                occlusion += muffler.Weight;
        }

        return occlusion;
    }

    private static bool Crosses(Muffler muffler, Vector2 start, Vector2 direction, float length)
    {
        var ray = new Ray(muffler.InverseRotation.RotateVec(start - muffler.Position),
            muffler.InverseRotation.RotateVec(direction));

        return ray.Intersects(muffler.Bounds, out var distance, out _) && distance <= length;
    }

    private Box2? GetLocalBounds(EntityUid uid)
    {
        if (!_fixturesQuery.TryComp(uid, out var fixtures))
            return null;

        Box2? bounds = null;
        var origin = new Transform(Vector2.Zero, Angle.Zero);
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var box = fixture.Shape.ComputeAABB(origin, i);
                bounds = bounds?.Union(box) ?? box;
            }
        }

        return bounds;
    }

    private float ObstacleWeight(EntityUid uid)
    {
        if (_doorQuery.HasComp(uid))
            return DoorWeight;

        if (_occluderQuery.TryComp(uid, out var occluder) && occluder.Enabled)
            return WallWeight;

        if (_tag.HasTag(uid, WindowTag))
            return WindowWeight;

        return FlimsyWeight;
    }

    private bool IsOwnedBy(EntityUid uid, EntityUid owner)
    {
        for (var i = 0; i < OwnSearchDepth && uid.IsValid(); i++)
        {
            if (uid == owner)
                return true;

            if (!_xformQuery.TryComp(uid, out var xform))
                return false;

            uid = xform.ParentUid;
        }

        return false;
    }

    private SourceInfo? Nearest(MapCoordinates position, EntityUid? parent, float range)
    {
        SourceInfo? best = null;
        var bestDistance = range * range;

        foreach (var source in _sources)
        {
            if (source.Position.MapId != position.MapId || parent != null && source.Parent != parent)
                continue;

            var distance = (source.Position.Position - position.Position).LengthSquared();
            if (distance > bestDistance)
                continue;

            best = source;
            bestDistance = distance;
        }

        return best;
    }

    private Placement GetOpenSpace(MapCoordinates position)
    {
        if (!_mapManager.TryFindGridAt(position.MapId, position.Position, out var gridUid, out var gridComp))
            return new Placement(true, null);

        var empty = !_maps.TryGetTileRef(gridUid, gridComp, position.Position, out var tile) || tile.Tile.IsEmpty;
        return new Placement(empty, gridUid);
    }
}

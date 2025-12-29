using System.Numerics;
using Content.Omu.Server.Voidwalker.Kidnapping.Voided;
using Content.Server.Respawn;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Gibbing.Systems;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Voidwalker.Kidnapping;

public sealed class VoidwalkerKidnappedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SpecialRespawnSystem _respawn = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly GibbingSystem _gib = default!;

    private const int MaxTeleportAttempts = 100;
    private ISawmill _sawmill = null!;

    private Dictionary<EntityUid, int> _teleportFailCount = new();
    private const int MaxTeleportAttemptFails = 10;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("voidwalker-kidnapping");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var kidnappedQuery = EntityQueryEnumerator<VoidwalkerKidnappedComponent>();
        while (kidnappedQuery.MoveNext(out var uid, out var kidnapped))
        {
            if (_timing.CurTime < kidnapped.ExitVoidTime
                || !_mind.TryGetMind(uid, out _, out var mind))
                continue;

            mind.PreventGhosting = false;

            if (TryTeleportToRandomPartOfStation(uid, Transform(kidnapped.OriginalMap)))
                RemCompDeferred(uid, kidnapped);
        }
    }

    public bool TryTeleportToRandomPartOfStation(EntityUid uid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return false;

        var station = _station.GetStationInMap(xform.MapID);

        if (!TryComp<StationDataComponent>(station, out var stationData))
            return false;

        var entityGridUid = _station.GetLargestGrid(stationData);

        if (entityGridUid is null
            || xform.MapUid is null)
            return false;

        if (_respawn.TryFindRandomTile(entityGridUid.Value, xform.MapUid.Value, MaxTeleportAttempts, out var randomPos))
            _transform.SetCoordinates(uid, randomPos);
        else
        {
            _teleportFailCount[uid] += 1;
            if (_teleportFailCount[uid] >= MaxTeleportAttemptFails)
            {
                _sawmill.Warning($"Could not find station to return {ToPrettyString(uid)} to within {MaxTeleportAttempts * MaxTeleportAttemptFails} attempts. Deleting.");
                Del(uid);
                return false;
            }

            var mapCoordinates = new MapCoordinates(new Vector2(0, 0), xform.MapID);
            _transform.SetMapCoordinates(uid, mapCoordinates);
            _sawmill.Warning($"Could not find station to return {ToPrettyString(uid)} to within {MaxTeleportAttempts}. Returning to default position.");
        }

        _stun.KnockdownOrStun(uid, TimeSpan.FromSeconds(5), true); // whatever, go my magic number
        _popup.PopupEntity(Loc.GetString("voidwalker-kidnap-return"), uid, uid);

        return true;
    }
}

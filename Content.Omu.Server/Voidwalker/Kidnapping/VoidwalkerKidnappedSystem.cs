using Content.Omu.Server.Voidwalker.Kidnapping.Voided;
using Content.Server.Respawn;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
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

            TeleportToRandomPartOfStation(uid, Transform(kidnapped.OriginalMap));
            RemCompDeferred(uid, kidnapped);
        }
    }

    public void TeleportToRandomPartOfStation(EntityUid uid, TransformComponent xform)
    {
        var station = _station.GetStationInMap(xform.MapID);

        if (!TryComp<StationDataComponent>(station, out var stationData))
            return;

        var entityGridUid = _station.GetLargestGrid(stationData);

        if (entityGridUid is null
            || xform.MapUid is null)
            return;

        _respawn.TryFindRandomTile(entityGridUid.Value, xform.MapUid.Value, 10, out var randomPos);
        _transform.SetCoordinates(uid, randomPos);

        _stun.KnockdownOrStun(uid, TimeSpan.FromSeconds(5), true); // whatever, go my magic number
        _popup.PopupEntity(Loc.GetString("voidwalker-kidnap-return"), uid, uid);
    }

    public void TeleportToRandomPartOfStation(EntityUid uid)
    {
        var xform = Transform(uid);
        var station = _station.GetStationInMap(xform.MapID);

        if (!TryComp<StationDataComponent>(station, out var stationData))
            return;

        var entityGridUid = _station.GetLargestGrid(stationData);

        if (entityGridUid is null
            || xform.MapUid is null)
            return;

        _respawn.TryFindRandomTile(entityGridUid.Value, xform.MapUid.Value, 10, out var randomPos);
        _transform.SetCoordinates(uid, randomPos);
    }
}

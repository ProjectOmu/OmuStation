using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Server.Power.Components;
using Content.Omu.Server.StationEvents.Components;
using Content.Server.Shuttles.Components;
using Content.Shared.Damage;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Station.Components;
using Content.Shared.Pinpointer;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Omu.Server.StationEvents.Events;

public sealed partial class LightsOutMinorRule : LightsOutRule<LightsOutMinorRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void Started(EntityUid uid, LightsOutMinorRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // let the smashing start 5 seconds after the announcement goes out
        component.SmashingTime = _timing.CurTime + TimeSpan.FromSeconds(5);

        // if there's no station, we can't run this event
        if (!TryGetRandomStation(out var stationP))
            return;

        var station = stationP.Value; // make this not null, god

        // choose a random station beacon to center the breaking around
        var beacons = new List<EntityUid>();
        using var beaconsEqe = EntityQueryEnumerator<ConfigurableNavMapBeaconComponent>();
        while (beaconsEqe.MoveNext(out var beacon, out _))
        {
            // don't consider a beacon if it's off-station
            if (!IsEntityOnStation(beacon, station))
                continue;

            beacons.Add(beacon);
        }

        // if there's no station beacon, we can't run this event
        if (beacons.Count == 0)
            return;

        var centerPosition = Transform(beacons[_random.Next(beacons.Count)]).LocalPosition;

        // generate the list of targets (and store list of all lights to make major and minor versions initially indistinguishable)
        using var allLights = EntityQueryEnumerator<PoweredLightComponent>();
        while (allLights.MoveNext(out var light, out _))
        {
            // don't target if the light isn't powered
            if (TryComp<ApcPowerReceiverComponent>(light, out var powerComp)
                && !powerComp.Powered)
                continue;

            // don't target if the light isn't on the station
            if (!IsEntityOnStation(light, station))
                continue;

            component.AllLights.Add(light);

            // target the lights in a 20 tile radius around the chosen beacon
            var lightPosition = Transform(light).LocalPosition;
            if (Vector2.Distance(centerPosition, lightPosition) < 20.0)
                component.Targets.Add(light);
        }

        _chat.DispatchStationAnnouncement(
            station,
            Loc.GetString("lights-out-announcement"),
            Loc.GetString("lights-out-sender"),
            playDefaultSound: true,
            colorOverride:
            Color.FromHex("#f9a524")
        );
    }

    protected override void ActiveTick(EntityUid uid, LightsOutMinorRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.SmashingTime == null)
            return;

        InteriorActiveTick(
            _timing,
            _random,
            _ghost,
            _damageable,
            ref component.SmashingTime,
            component.AllLights,
            component.Targets,
            ref component.TargetIndex,
            component.Damage,
            component.DamageProbability
        );
    }
}

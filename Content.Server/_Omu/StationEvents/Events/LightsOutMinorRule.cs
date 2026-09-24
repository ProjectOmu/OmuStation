using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Server.Power.Components;
using Content.Server._Omu.StationEvents.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Station.Components;
using Content.Shared.Pinpointer;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Random;

namespace Content.Server._Omu.StationEvents.Events;

public sealed partial class LightsOutMinorRule : StationEventSystem<LightsOutMinorRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    protected override void Started(EntityUid uid, LightsOutMinorRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // let the smashing start 5 seconds after the announcement goes out
        component.SmashingTime = _timing.CurTime + TimeSpan.FromSeconds(5);

        component.Damage = new DamageSpecifier(_proto.Index<DamageGroupPrototype>("Brute"), 5);

        // if there's no station, we can't run this event
        if (!TryGetRandomStation(out var station))
            return;

        // choose a random station beacon to center the breaking around
        var beacons = new List<EntityUid>();
        var beacons_eqe = EntityQueryEnumerator<ConfigurableNavMapBeaconComponent>();
        while (beacons_eqe.MoveNext(out var beacon, out _))
        {
            beacons.Add(beacon);
        }
        // if there's no station beacon, we can't run this event
        if (beacons.Count == 0)
            return;
        var center_position = Transform(beacons[_random.Next(beacons.Count)]).LocalPosition;

        // generate the list of targets (and store list of all lights to make major and minor versions initially indistinguishable)
        var all_lights = EntityQueryEnumerator<PoweredLightComponent>();
        while (all_lights.MoveNext(out var light, out _))
        {
            // don't target if the light isn't powered
            if (TryComp<ApcPowerReceiverComponent>(light, out var powerComp)
                && powerComp != null // compiler complains otherwise
                && !powerComp.Powered)
                continue;

            // don't target if the light isn't on the station
            var transform = Transform(light);
            if (!HasComp<BecomesStationComponent>(transform.GridUid)
                && CompOrNull<StationMemberComponent>(transform.GridUid)?.Station != station)
                continue;

            component.AllLights.Add(light);

            // target the lights in a 20 tile radius around the chosen beacon
            var light_position = Transform(light).LocalPosition;
            if (Vector2.Distance(center_position, light_position) < 20.0)
                component.Targets.Add(light);
        }
        component.TargetListLength = component.Targets.Count;

        _chat.DispatchStationAnnouncement(
            (EntityUid) station,
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

        if (_timing.CurTime < component.SmashingTime)
        {
            // lights flicker before the destruction starts. build suspense.
            foreach (EntityUid light in component.AllLights)
            {
                if (!_random.Prob(0.25f))
                    continue;
                _ghost.DoGhostBooEvent(light);
            }
        }
        else
        {
            // now, the destruction, one light at a time
            if (component.TargetIndex < component.TargetListLength)
            {
                if (_random.Prob(component.DamageProbability))
                    _damageable.TryChangeDamage(
                        component.Targets[component.TargetIndex],
                        component.Damage,
                        true
                    );
                component.TargetIndex++;
            } else {
                // finished smashing
                component.SmashingTime = null;
            }
        }

    }
}

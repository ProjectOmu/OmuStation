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
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Random;

namespace Content.Server._Omu.StationEvents.Events;

public sealed partial class LightsOutRule : StationEventSystem<LightsOutRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    protected override void Started(EntityUid uid, LightsOutRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // let the smashing start 5 seconds after the announcement goes out
        component.SmashingTime = _timing.CurTime + TimeSpan.FromSeconds(5);

        // i assume there is a station, and that the station the players are on is the first in the list
        var station = _station.GetStations()[0];

        // generate the list of targets
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

            component.Targets.Add(light);
        }
        component.TargetListLength = component.Targets.Count;

        _chat.DispatchStationAnnouncement(
            station,
            Loc.GetString("lights-out-announcement"),
            Loc.GetString("lights-out-sender"),
            playDefaultSound: true,
            colorOverride:
            Color.FromHex("#f9a524")
        );
    }

    protected override void ActiveTick(EntityUid uid, LightsOutRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.SmashingTime == null)
            return;

        if (_timing.CurTime < component.SmashingTime)
        {
            // lights flicker before the destruction starts. build suspense.
            foreach (EntityUid light in component.Targets)
            {
                if (!_random.Prob(0.25f))
                    continue;
                _ghost.DoGhostBooEvent(light);
            }
        }
        else
        {
            var damage = new DamageSpecifier(_proto.Index<DamageGroupPrototype>("Brute"), 5);
            // now, the destruction, one light at a time
            if (component.TargetIndex < component.TargetListLength)
            {
                if (_random.Prob(component.DamageProbability))
                    _damageable.TryChangeDamage(
                        component.Targets[component.TargetIndex],
                        damage,
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

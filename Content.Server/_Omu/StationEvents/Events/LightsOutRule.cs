using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Server.Power.Components;
using Content.Server._Omu.StationEvents.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
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

        // let the smashing start 10 seconds after the announcement goes out
        component.SmashingTime = _timing.CurTime + TimeSpan.FromSeconds(10);

        // i assume there is a station, and that the station the players are on is the first in the list
        var station = _station.GetStations()[0];

        // generate the list of targets
        var all_lights = EntityQueryEnumerator<PoweredLightComponent>();
        while (all_lights.MoveNext(out var light, out _))
        {
            // TODO: `continue` if the light isn't powered; see ApcPowerReceiver.Powered
            // don't target if the light isn't powered


            // TODO: `continue` if the light isn't on the station
            // don't target if the light isn't on the station


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
            // now, the destruction
            foreach (EntityUid light in component.Targets)
            {
                if (!_random.Prob(component.DamageProbability))
                    continue;
                _damageable.TryChangeDamage(light, damage, true);
            }
            // finish smashing
            component.SmashingTime = null;
        }

    }
}

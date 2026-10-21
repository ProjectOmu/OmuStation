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
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Omu.Server.StationEvents.Events;

public sealed partial class LightsOutMajorRule : LightsOutRule<LightsOutMajorRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void Started(EntityUid uid, LightsOutMajorRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // let the smashing start 5 seconds after the announcement goes out
        component.SmashingTime = _timing.CurTime + TimeSpan.FromSeconds(5);

        // if there's no station, we can't run this event
        if (!TryGetRandomStation(out var station))
            return;

        // generate the list of targets
        using var allLights = EntityQueryEnumerator<PoweredLightComponent>();
        while (allLights.MoveNext(out var light, out _))
        {
            // don't target if the light isn't powered
            if (TryComp<ApcPowerReceiverComponent>(light, out var powerComp)
                && !powerComp.Powered)
                continue;

            if (!IsEntityOnStation(light, station.Value))
                continue;

            component.Targets.Add(light);
        }

        _chat.DispatchStationAnnouncement(
            station.Value,
            Loc.GetString("lights-out-announcement"),
            Loc.GetString("lights-out-sender"),
            playDefaultSound: true,
            colorOverride:
            Color.FromHex("#f9a524")
        );
    }

    protected override void ActiveTick(EntityUid uid, LightsOutMajorRuleComponent component, GameRuleComponent gameRule, float frameTime)
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
            component.Targets,
            component.Targets,
            ref component.TargetIndex,
            component.Damage,
            component.DamageProbability
        );
    }
}

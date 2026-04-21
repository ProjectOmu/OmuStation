using System.Linq;
using Content.Omu.Server.GameDirector.Metric.Components;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Doors.Components;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Common.StationEvent.Metrics;
namespace Content.Omu.Server.GameDirector.Metric;

/// <summary>
///   Uses doors and firelocks to sample station chaos across the station
///
///   Emag - EmagCost per emaged door
///   Power - PowerCost per door or firelock with no power
///   Atmos - PressureCost for holding spacing or FireCost for holding back fire
/// </summary>
public sealed class DoorMetricSystem : ChaosMetricSystem<DoorMetricComponent>
{
    [Dependency] private readonly StationSystem _stationSystem = default!;

    protected override ChaosMetrics CalculateChaos(
        EntityUid metricUid,
        DoorMetricComponent component,
        CalculateChaosEvent args)
    {
        var firelockQ = GetEntityQuery<FirelockComponent>();
        var airlockQ = GetEntityQuery<AirlockComponent>();

        double doorCounter = 0;
        double firelockCounter = 0;
        double airlockCounter = 0;
        double fireCount = 0;
        double pressureCount = 0;
        double emagWeightedCount = 0;
        double powerCount = 0;

        // Add up the pain of all the doors
        // Restrict to just doors on the main station
        var stationGrids = _stationSystem.GoobGetAllStationGrids();

        var queryFirelock = EntityQueryEnumerator<DoorComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (queryFirelock.MoveNext(out var uid, out var door, out var power, out var transform))
        {
            if (transform.GridUid == null || !stationGrids.Contains(transform.GridUid.Value))
                continue;

            fireCount = CalculateFirelock(firelockQ, uid, fireCount, ref pressureCount, ref firelockCounter);
            emagWeightedCount = CalculateAirlock(airlockQ, uid, door, component, emagWeightedCount, ref airlockCounter);
            powerCount = CalculateDoorPower(power, powerCount, ref doorCounter);
        }

        double emagChaos = 0;
        double atmosChaos = 0;
        double powerChaos = 0;
        // Calculate each stat as a fraction of all doors in the station.
        // That way the metrics do not "scale up"  on large stations.

        if (airlockCounter > 0)
            emagChaos = Math.Round((emagWeightedCount / airlockCounter) * component.EmagCost);
        if (firelockCounter > 0)
            atmosChaos = Math.Round(fireCount / firelockCounter * component.FireCost
                                    + pressureCount / firelockCounter * component.PressureCost);
        if (doorCounter > 0)
            powerChaos = Math.Round(powerCount / doorCounter * component.PowerCost);

        var chaos = new ChaosMetrics(new Dictionary<ChaosMetric, double>()
        {
            {ChaosMetric.Security, emagChaos},
            {ChaosMetric.Atmos, atmosChaos},
            {ChaosMetric.Power, powerChaos},
        });
        return chaos;
    }

    private static double CalculateDoorPower(ApcPowerReceiverComponent power, double powerCount, ref double doorCounter)
    {
        if (power is { NeedsPower: true, Powered: false })
            powerCount += 1;

        doorCounter += 1;
        return powerCount;
    }

    private double CalculateAirlock(EntityQuery<AirlockComponent> airlockQ,
        EntityUid uid,
        DoorComponent door,
        DoorMetricComponent component,
        double emagWeightedCount,
        ref double airlockCounter)
    {
        if (!airlockQ.TryGetComponent(uid, out var airlock))
            return emagWeightedCount;
        if (door.State == DoorState.Emagging)
        {
            var modifier = GetAccessLevelModifier(uid, component);
            emagWeightedCount += 1 + modifier;
        }

        airlockCounter += 1;

        return emagWeightedCount;
    }

    private static double CalculateFirelock(EntityQuery<FirelockComponent> firelockQ,
        EntityUid uid,
        double fireCount,
        ref double pressureCount,
        ref double firelockCounter)
    {
        if (!firelockQ.TryGetComponent(uid, out var firelock))
            return fireCount;

        if (firelock.Temperature)
            fireCount += 1;
        else if (firelock.Pressure)
            pressureCount += 1;

        firelockCounter += 1;

        return fireCount;
    }

    private int GetAccessLevelModifier(EntityUid uid, DoorMetricComponent component)
    {
        if (!TryComp<AccessReaderComponent>(uid, out var accessReaderComponent))
            return 0;

        var modifier = 0;
        // index 0 is the primary access set by engine contract (AccessReader always puts it first)
        var accessSet = accessReaderComponent.AccessLists.ElementAt(0);
        foreach (var accessPrototype in accessSet)
        {
            modifier += component.AccessWeights.GetValueOrDefault(accessPrototype.Id, 0);
        }
        return modifier;
    }
}

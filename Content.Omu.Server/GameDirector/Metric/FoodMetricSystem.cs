using Content.Omu.Server.GameDirector.Metric.Components;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Goobstation.Common.StationEvent.Metrics;
namespace Content.Omu.Server.GameDirector.Metric;

/// <summary>
///   Measure crew's hunger, thirst and charge
/// </summary>
public sealed class FoodMetricSystem : ChaosMetricSystem<FoodMetricComponent>
{
    protected override ChaosMetrics CalculateChaos(EntityUid metricUid, FoodMetricComponent component, CalculateChaosEvent args)
    {
        // Gather hunger and thirst scores
        var query = EntityQueryEnumerator<MindContainerComponent, MobStateComponent>();
        double hungerSc = 0;
        double thirstSc = 0;
        double chargeSc = 0;

        var thirstQ = GetEntityQuery<ThirstComponent>();
        var hungerQ = GetEntityQuery<HungerComponent>();
        var siliconQ = GetEntityQuery<SiliconComponent>();

        while (query.MoveNext(out var uid, out var mindContainer, out var mobState))
        {
            // Don't count anything that is mindless, do count antags
            if (mindContainer.Mind == null)
                continue;

            if (mobState.CurrentState != MobState.Alive)
                continue;
            thirstSc = CalculateThirst(component, thirstQ, uid, thirstSc);
            hungerSc = CalculateHunger(component, hungerQ, uid, hungerSc);
            chargeSc = CalculateCharge(component, siliconQ, uid, chargeSc);
        }

        var chaos = new ChaosMetrics(new Dictionary<ChaosMetric, double>()
        {
            {ChaosMetric.Hunger, hungerSc},
            {ChaosMetric.Thirst, thirstSc},
            {ChaosMetric.Charge, chargeSc},
        });
        return chaos;
    }

    private double CalculateCharge(FoodMetricComponent component,
        EntityQuery<SiliconComponent> siliconQ,
        EntityUid uid,
        double chargeSc)
    {
        if (!siliconQ.TryGetComponent(uid, out var silicon))
            return chargeSc;
        var chargeStateValue = GetChargeState(silicon.ChargeState, component);
        chargeSc += component.ChargeScores.GetValueOrDefault(chargeStateValue).Double();

        return chargeSc;
    }

    private static double CalculateHunger(FoodMetricComponent component, EntityQuery<HungerComponent> hungerQ, EntityUid uid,
        double hungerSc)
    {
        if (!hungerQ.TryGetComponent(uid, out var hunger))
            return hungerSc;
        var threshold = hunger.CurrentThreshold;
        hungerSc += component.HungerScores.GetValueOrDefault(threshold).Double();
        return hungerSc;
    }

    private static double CalculateThirst(FoodMetricComponent component,
        EntityQuery<ThirstComponent> thirstQ,
        EntityUid uid, double thirstSc)
    {
        if (!thirstQ.TryGetComponent(uid, out var thirst))
            return thirstSc;
        var threshold = thirst.CurrentThirstThreshold;
        thirstSc += component.ThirstScores.GetValueOrDefault(threshold).Double();

        return thirstSc;
    }

    // silicon ChargeState is a short from 0-10, so we divide by 10 to normalize to 0.0-1.0
    private const float ChargeStateNormalizeDivisor = 10f;

    private static float GetChargeState(short chargeState, FoodMetricComponent component)
    {
        var normalizedCharge = chargeState / ChargeStateNormalizeDivisor;

        // returns the exact key used in ChargeScores so GetValueOrDefault always finds a match
        if (normalizedCharge <= component.ChargeCriticalThreshold)
            return component.ChargeCriticalThreshold;
        if (normalizedCharge <= component.ChargeLowThreshold)
            return component.ChargeLowThreshold;
        return component.ChargeMidThreshold;
    }

}

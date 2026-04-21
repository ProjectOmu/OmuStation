using Content.Server.Spreader;
using Content.Shared.Anomaly.Components;
using Content.Goobstation.Common.StationEvent.Metrics;

namespace Content.Omu.Server.GameDirector.Metric;

/// <summary>
///   Measures the number and severity of anomalies on the station.
///   Writes this to the Anomaly chaos value.
/// </summary>
public sealed class AnomalyMetric : ChaosMetricSystem<Components.AnomalyMetricComponent>
{
    protected override ChaosMetrics CalculateChaos(EntityUid metricUid,
        Components.AnomalyMetricComponent component,
        CalculateChaosEvent args)
    {
        double anomalyChaos = 0;

        // consider each anomaly and add its stability and growth to the accumulator
        var anomalyQ = EntityQueryEnumerator<AnomalyComponent>();
        while (anomalyQ.MoveNext(out _, out var anomaly))
        {
            if (anomaly.Severity > component.SeverityThreshold)
                anomalyChaos += component.SeverityCost;

            if (anomaly.Stability > anomaly.GrowthThreshold)
                anomalyChaos += component.GrowingCost;

            anomalyChaos += component.BaseCost;
        }

        var kudzuQ = EntityQueryEnumerator<KudzuComponent>();
        while (kudzuQ.MoveNext(out _, out _))
        {
            anomalyChaos += component.KudzuCost;
        }

        return new ChaosMetrics(new Dictionary<ChaosMetric, double>
        {
            { ChaosMetric.Anomaly, anomalyChaos },
        });
    }
}

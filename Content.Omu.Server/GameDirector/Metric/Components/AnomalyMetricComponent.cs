namespace Content.Omu.Server.GameDirector.Metric.Components;

[RegisterComponent, Access(typeof(AnomalyMetric))]
public sealed partial class AnomalyMetricComponent : Component
{
    /// <summary>
    ///   Cost of a growing anomaly
    /// </summary>
    [DataField]
    public float GrowingCost = 40.0f;

    /// <summary>
    ///   Cost of a dangerous anomaly
    /// </summary>
    [DataField]
    public float SeverityCost = 20.0f;

    /// <summary>
    ///   Cost of any anomaly
    /// </summary>
    [DataField("dangerCost")]
    public float BaseCost = 10.0f;

    /// <summary>
    ///   Chaos cost added per kudzu patch
    /// </summary>
    [DataField]
    public float KudzuCost = 0.25f;

    /// <summary>
    ///   Severity value above which an anomaly counts as severe
    /// </summary>
    [DataField]
    public float SeverityThreshold = 0.8f;
}

namespace Content.Omu.Server.GameDirector.Metric.Components;

[RegisterComponent, Access(typeof(DoorMetricSystem))]
public sealed partial class DoorMetricComponent : Component
{
    /// <summary>
    ///   Cost of all doors emagged door
    /// </summary>
    [DataField]
    public double EmagCost = 200.0f;

    /// <summary>
    ///   Cost of all doors with no power
    /// </summary>
    [DataField]
    public double PowerCost = 100.0f;

    /// <summary>
    ///   Cost of all firedoors holding pressure
    /// </summary>
    [DataField]
    public double PressureCost = 200.0f;

    /// <summary>
    ///   Cost of all firedoors holding temperature
    /// </summary>
    [DataField]
    public double FireCost = 400.0f;

    /// <summary>
    ///   extra weight added per emag based on door access level.
    ///   higher access doors are worth more chaos when emagged.
    /// </summary>
    [DataField]
    public Dictionary<string, int> AccessWeights = new()
    {
        { "Security",     1 },
        { "Atmospherics", 1 },
        { "Command",      2 },
        { "Armory",       3 },
    };
}

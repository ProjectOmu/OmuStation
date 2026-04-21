using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Omu.Server.GameDirector.Metric.Components;

[RegisterComponent, Access(typeof(FoodMetricSystem))]
public sealed partial class FoodMetricComponent : Component
{
    [DataField(customTypeSerializer: typeof(DictionarySerializer<ThirstThreshold, FixedPoint2>))]
    public Dictionary<ThirstThreshold, FixedPoint2> ThirstScores =
        new()
        {
            { ThirstThreshold.Thirsty, 2.0f },
            { ThirstThreshold.Parched, 5.0f },
        };

    [DataField(customTypeSerializer: typeof(DictionarySerializer<HungerThreshold, FixedPoint2>))]
    public Dictionary<HungerThreshold, FixedPoint2> HungerScores =
        new()
        {
            { HungerThreshold.Peckish, 2.0f },
            { HungerThreshold.Starving, 5.0f },
        };

    /// <summary>
    ///   normalized charge threshold below which a silicon counts as critical (matches ChargeCriticalThreshold key)
    /// </summary>
    [DataField]
    public float ChargeCriticalThreshold = 0.10f;

    /// <summary>
    ///   normalized charge threshold below which a silicon counts as low (matches ChargeLowThreshold key)
    /// </summary>
    [DataField]
    public float ChargeLowThreshold = 0.35f;

    /// <summary>
    ///   return value used when charge is above low - also the key for the mid entry in ChargeScores
    /// </summary>
    [DataField]
    public float ChargeMidThreshold = 0.80f;

    /// <summary>
    ///   chaos score per silicon at each charge bucket.
    ///   keys must match ChargeCriticalThreshold, ChargeLowThreshold, ChargeMidThreshold exactly.
    /// </summary>
    [DataField(customTypeSerializer: typeof(DictionarySerializer<float, FixedPoint2>))]
    public Dictionary<float, FixedPoint2> ChargeScores =
        new()
        {
            { 0.80f, 1.0f },
            { 0.35f, 2.0f },
            { 0.10f, 5.0f },
        };
}

using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Goobstation.Common.StationEvent.Metrics;

// ideally this would live in server-only, but StationEventComponent (Content.Server) uses ChaosMetrics
// and Content.Server does not reference Content.Goobstation.Server or Content.Omu.Server.
// Content.Goobstation.Common is the lowest common denominator visible to both Content.Server and Content.Omu.Server.
public enum ChaosMetric
{
    Hunger,
    Thirst,
    Charge,

    Anomaly,

    Friend,   // negative score for how many friendlies on station
    Hostile,  // increases with hostiles

    Death,
    Medical,

    Security,
    Power,
    Atmos,
    Mess,

    // metrics calculated from above by the game director:
    Combat,   // friend + hostile - <0 if crew is strong, 0 if balanced, >0 if crew is losing
}

/// <summary>
///   raised to request metrics to calculate and sum their statistics
/// </summary>
[ByRefEvent]
public record struct CalculateChaosEvent(ChaosMetrics Metrics);

/// <summary>
///   represents a collection of chaos values, one per ChaosMetric.
///   similar in design to DamageSpecifier.
/// </summary>
[DataDefinition]
public sealed partial class ChaosMetrics : IEquatable<ChaosMetrics>
{
    [IncludeDataField(customTypeSerializer: typeof(DictionarySerializer<ChaosMetric, double>)), ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<ChaosMetric, double> ChaosDict { get; set; } = new();

    public bool Empty => ChaosDict.Count == 0;

    public bool AnyWorseThan(ChaosMetrics other) => ChaosDict.Any(kvp =>
        other.ChaosDict.TryGetValue(kvp.Key, out var otherV)
        && kvp.Value > otherV);

    public bool AllBetterThan(ChaosMetrics other) => ChaosDict.All(kvp =>
        !other.ChaosDict.TryGetValue(kvp.Key, out var otherV)
        || kvp.Value < otherV);

    public ChaosMetrics() { }

    public ChaosMetrics(ChaosMetrics chaos)
    {
        ChaosDict = new(chaos.ChaosDict);
    }

    public ChaosMetrics(Dictionary<ChaosMetric, double> chaos)
    {
        ChaosDict = new(chaos);
    }

    public override string? ToString()
    {
        return String.Join(", ", ChaosDict.Select(p => $"{p.Key}: {p.Value}"));
    }

    public void ClampMin(double minValue)
    {
        foreach (var (key, value) in ChaosDict)
        {
            if (value < minValue)
                ChaosDict[key] = minValue;
        }
    }

    public void ClampMax(double maxValue)
    {
        foreach (var (key, value) in ChaosDict)
        {
            if (value > maxValue)
                ChaosDict[key] = maxValue;
        }
    }

    public ChaosMetrics ExclusiveSubtract(ChaosMetrics other)
    {
        ChaosMetrics result = new(ChaosDict.ShallowClone());
        foreach (var (type, value) in other.ChaosDict)
        {
            if (result.ChaosDict.ContainsKey(type))
                result.ChaosDict[type] -= value;
        }
        return result;
    }

    public static ChaosMetrics operator +(ChaosMetrics chaosA, ChaosMetrics chaosB)
    {
        ChaosMetrics result = new(chaosA);
        foreach (var entry in chaosB.ChaosDict)
        {
            if (!result.ChaosDict.TryAdd(entry.Key, entry.Value))
                result.ChaosDict[entry.Key] += entry.Value;
        }
        return result;
    }

    public static ChaosMetrics operator -(ChaosMetrics chaosA, ChaosMetrics chaosB)
    {
        ChaosMetrics result = new(chaosA);
        foreach (var entry in chaosB.ChaosDict)
        {
            if (!result.ChaosDict.TryAdd(entry.Key, -entry.Value))
                result.ChaosDict[entry.Key] -= entry.Value;
        }
        return result;
    }

    public bool Equals(ChaosMetrics? other)
    {
        if (other == null || ChaosDict.Count != other.ChaosDict.Count)
            return false;

        foreach (var (key, value) in ChaosDict)
        {
            if (!other.ChaosDict.TryGetValue(key, out var otherValue) || value != otherValue)
                return false;
        }
        return true;
    }
}

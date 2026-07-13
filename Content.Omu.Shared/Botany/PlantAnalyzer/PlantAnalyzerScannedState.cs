using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Omu.Shared.Botany.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerScannedState : BoundUserInterfaceState
{
    // Basic
    public PlantAnalyzerStatus Status;
    public string? Name;
    public PlantAnalyzerScanType ScanType;
    public NetEntity? Entity;
    public bool HarvestReady;
    private PlantAnalyzerStatus _active;

    // Condition
    public float? Health;
    public float? MaxHealth;
    public float? Water;
    public float? Nutrition;
    public bool Dead;
    public float? ToxinLevel;
    public float? WeedLevel;
    public float? PestLevel;

    // Genetics
    public int? Yield;
    public List<string>? MutationTargets;

    // Text
    public string? WarningsText;
    public string? ResistancesText;
    public string? MutationsText;
    public string? AllTraitsText;

    // Chemistry
    public List<PlantReagentEntry>? InherentReagents;
    public List<PlantReagentEntry>? MutatedReagents;
    public List<PlantReagentEntry>? ConsumedGases;
    public List<PlantReagentEntry>? ExudedGases;

    // Additional genetics
    public float? Potency;
    public float? Maturation;
    public float? Production;
    public float? PestResistance;
    public float? ToxinResistance;
    public float? WeedResistance;

    // Environment requirements
    public float? IdealTemperature;
    public float? TemperatureTolerance;
    public float? LowPressureTolerance;
    public float? HighPressureTolerance;
    public List<string>? MissingGases;

    public PlantAnalyzerScannedState(PlantAnalyzerStatus active, string name)
    {
        _active = active;
        Name = name;
    }

    public PlantAnalyzerScannedState(
        PlantAnalyzerStatus status,
        string? name,
        PlantAnalyzerScanType scanType,
        NetEntity? entity,
        float? health,
        float? maxHealth,
        float? water,
        float? nutrition,
        bool dead,
        float? toxinLevel = null,
        float? weedLevel = null,
        float? pestLevel = null,
        int? yield = null,
        List<string>? mutationTargets = null,
        string? warningsText = null,
        string? resistancesText = null,
        string? mutationsText = null,
        string? allTraitsText = null,
        List<PlantReagentEntry>? inherentReagents = null,
        List<PlantReagentEntry>? mutatedReagents = null,
        List<PlantReagentEntry>? consumedGases = null,
        List<PlantReagentEntry>? exudedGases = null,
        float? potency = null,
        float? maturation = null,
        float? production = null,
        float? pestResistance = null,
        float? toxinResistance = null,
        float? weedResistance = null,
        bool harvestReady = false,
        float? idealTemperature = null,
        float? temperatureTolerance = null,
        float? lowPressureTolerance = null,
        float? highPressureTolerance = null,
        List<string>? missingGases = null)
    {
        Status = status;
        Name = name;
        ScanType = scanType;
        Entity = entity;

        Health = health;
        MaxHealth = maxHealth;
        Water = water;
        Nutrition = nutrition;
        Dead = dead;

        ToxinLevel = toxinLevel;
        WeedLevel = weedLevel;
        PestLevel = pestLevel;

        Yield = yield;
        MutationTargets = mutationTargets;

        WarningsText = warningsText;
        ResistancesText = resistancesText;
        MutationsText = mutationsText;
        AllTraitsText = allTraitsText;

        InherentReagents = inherentReagents;
        MutatedReagents = mutatedReagents;
        ConsumedGases = consumedGases;
        ExudedGases = exudedGases;

        Potency = potency;
        Maturation = maturation;
        Production = production;
        PestResistance = pestResistance;
        ToxinResistance = toxinResistance;
        WeedResistance = weedResistance;

        IdealTemperature = idealTemperature;
        TemperatureTolerance = temperatureTolerance;
        LowPressureTolerance = lowPressureTolerance;
        HighPressureTolerance = highPressureTolerance;
        MissingGases = missingGases;

        HarvestReady = harvestReady;
    }
}

[Serializable, NetSerializable]
public enum PlantAnalyzerStatus : byte
{
    NoData,
    Active,
    OutOfRange,
}

public enum PlantAnalyzerScanType
{
    None,
    Seed,
    Plant,
    Produce
}

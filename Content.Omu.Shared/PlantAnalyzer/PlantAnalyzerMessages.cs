using Robust.Shared.Serialization;

namespace Content.Omu.Shared.PlantAnalyzer;

// local mirror of Content.Server.Botany.HarvestType - kept in sync manually.
// defined here because HarvestType lives in Content.Server and cannot be referenced from Shared.
[Serializable, NetSerializable]
public enum PlantAnalyzerHarvestType : byte
{
    NoRepeat,
    Repeat,
    SelfHarvest
}

// state sent to the client after a successful scan.
// all tabs are always populated - the analyzer is always in advanced mode.
[Serializable, NetSerializable]
public sealed class PlantAnalyzerScannedSeedMessage : BoundUserInterfaceState
{
    public NetEntity? TargetEntity;

    // true if scanned a plant tray, false if a loose seed
    public bool IsTray;

    // plant tray health status (only relevant when IsTray = true)
    public bool IsDead;
    public float PlantHealth;
    public float PlantMaxHealth; // = seed.Endurance

    // basic tab
    public string? SeedName;
    public int SeedYield;
    public float SeedPotency;
    public PlantAnalyzerHarvestType HarvestType;
    public string[] Chemicals = [];
    public string[] ExudeGases = [];
    public string[] ConsumeGases = [];
    public float Lifespan;
    public float Maturation;
    public float Production;
    public int GrowthStages;
    public float Endurance;

    // tolerances tab
    public float NutrientConsumption;
    public float WaterConsumption;
    public float IdealHeat;
    public float HeatTolerance;
    public float IdealLight;
    public float LightTolerance;
    public float ToxinsTolerance;
    public float LowPressureTolerance;
    public float HighPressureTolerance;
    public float PestTolerance;
    public float WeedTolerance;

    // mutations tab
    // active mutation flags (only flags for mutations that are actually in the base game)
    public PlantMutationFlags Mutations;
    // possible sub-species from mutation prototypes
    public string[] Speciation = [];
}

// only includes mutations that are actually implemented in the base botany system.
// slip and sentient are omitted intentionally - they have no base implementation.
[Flags, Serializable, NetSerializable]
public enum PlantMutationFlags : byte
{
    None = 0,
    TurnIntoKudzu = 1,
    Seedless = 2,
    Ligneous = 4,
    CanScream = 8,
}

using Content.Shared.Botany;
using Robust.Shared.Serialization;

namespace Content.Shared._DV.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerScannedSeedMessage : BoundUserInterfaceState
{
    public NetEntity? TargetEntity;

    public bool IsTray;
    public bool IsDead;
    public float PlantHealth;
    public float PlantMaxHealth;

    public string SeedName = "";
    public int SeedYield;
    public float SeedPotency;
    public HarvestType HarvestType;
    public string[] Chemicals = [];
    public string[] ExudeGases = [];
    public string[] ConsumeGases = [];
    public float Lifespan;
    public float Maturation;
    public float Production;
    public int GrowthStages;
    public float Endurance;

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

    public string[] Mutations = [];
    public string[] Speciation = [];
}

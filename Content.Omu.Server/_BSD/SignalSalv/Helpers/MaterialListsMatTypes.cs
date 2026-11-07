//if you are asking why this exist.... there is no easy way to get a comprehensive list of ALL materials in the game. 
//SO here is the comprehensive list now.

//THIS_FILE_TO_ADD_NEW_MATERIALS
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.SignalSalv.Helpers;

//TODO expand material properties one mat sci gets added
public struct Material
{
    public Material(ProtoId<MaterialPrototype> type, float min, float max, bool minable, int significantDataCost, int latheGradeTransportCost = 1)
    {
        MaterialType = type;
        MinResoucePerSecond = min;
        MaxResoucePerSecond = max;
        Minable = minable;
        SignificantDataCostPerMiningUnit = significantDataCost;
        LatheGradeTransportCost = latheGradeTransportCost;
    }
    public ProtoId<MaterialPrototype> MaterialType { get; init; }
    public float MinResoucePerSecond { get; init; }// Amount in /100 per second || aka 1 -> 0.01/s 100 is 1/s
    public float MaxResoucePerSecond { get; init; }// Amount in /100 per second || aka 1 -> 0.01/s 100 is 1/s
    public bool Minable { get; init; }
    public int SignificantDataCostPerMiningUnit { get; init; } //cost for 0.01 /s outpust
    public int LatheGradeTransportCost { get; init; } //cost for 0.01 /s outpust
}

public struct CompleteMaterialList
{
    public HashSet<Material> BaseMaterials { get; init; }
    public HashSet<Material> AdvancedMaterials { get; init; }
    public HashSet<Material> SpecialMaterials { get; init; }
    public HashSet<Material> AllMaterials { get; init; }
    public HashSet<ProtoId<MaterialPrototype>> AllMaterialTypes { get; init; }
    public Dictionary<ProtoId<MaterialPrototype>, Material> AllMaterialsDict { get; init; }
    public CompleteMaterialList()
    {
        BaseMaterials = new();
        BaseMaterials.Add(new Material("Steel", 25, 50, true, 100));
        BaseMaterials.Add(new Material("Glass", 25, 50, true, 100));
        BaseMaterials.Add(new Material("Plastic", 0, 0, false, int.MaxValue));
        BaseMaterials.Add(new Material("Cloth", 0, 0, false, int.MaxValue));
        BaseMaterials.Add(new Material("DuraThread", 0, 0, false, int.MaxValue));
        AdvancedMaterials = new();
        AdvancedMaterials.Add(new Material("Gold", 10, 25, true, 500));
        AdvancedMaterials.Add(new Material("Silver", 10, 25, true, 500));
        AdvancedMaterials.Add(new Material("Uranium", 10, 25, true, 500));
        BaseMaterials.Add(new Material("BioMass", 10, 15, true, 2000));
        SpecialMaterials = new();
        SpecialMaterials.Add(new Material("Plasma", 0.1f, 1, true, 500000, 10));
        //yes 0.5 million is not a mistake you are not intended to do this
        //And only a tenth of the capacity so only 30 sheets can be transported at a time (PLASMA is rare anyway, prevents mass robbing of it)
        AllMaterials = new();
        AllMaterials.UnionWith(BaseMaterials);
        AllMaterials.UnionWith(AdvancedMaterials);
        AllMaterials.UnionWith(SpecialMaterials);
        AllMaterialTypes = new();
        AllMaterialsDict = new();
        foreach (var iterator in AllMaterials)
        {
            AllMaterialTypes.Add(iterator.MaterialType);
            AllMaterialsDict.Add(iterator.MaterialType, iterator);
        }
    }
}

using Content.Omu.Server._BSD.IngameServerSystem;
using Content.Omu.Server._BSD.SignalSalv.Components;
using Content.Omu.Server._BSD.SignalSalv.Events;
using Content.Omu.Server._BSD.SignalSalv.Helpers;

using Robust.Shared.Prototypes;
using Robust.Shared.Timing;




using Content.Shared.Materials;
using Content.Server.GameTicking.Events;

namespace Content.Omu.Server._BSD.SignalSalv;
/// <summary>
/// System that handles all mined materials and delivery to station, salves the materials inside the system!!!
/// </summary>
public sealed partial class BSDSignalSalvMaterialTransitSystem : EntitySystem, IBSDSignalSalvMaterialTransitSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BSDIngameServerSystem _ingameServer = default!;
    public Dictionary<ProtoId<MaterialPrototype>, int> MaterialInTransit = new();
    public int MaterialInTransitStorageCap = 100000;
    public Dictionary<ProtoId<MaterialPrototype>, float> MaterialProductionPerSecond = new();
    public EntityUid LinkedMaterialReciver = new();
    public TimeSpan LastUpdate = new();
    public static readonly CompleteMaterialList MaterialMiningRatesBase = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignalSalvMiningRigProductionChangeEvent>(UpdateProductionRates);
        SubscribeLocalEvent<RoundStartingEvent>(ResetSystem);
    }
    public void ResetSystem(RoundStartingEvent args)
    {
        MaterialInTransit = new();
        MaterialInTransitStorageCap = 100000;
        MaterialProductionPerSecond = new();
        LinkedMaterialReciver = new();
        LastUpdate = new();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        MaterialProductionTimeBased();
    }
    public void UpdateProductionRates(ref SignalSalvMiningRigProductionChangeEvent args)
    {
        foreach (var iterator in args.NewProductionRate.Keys)
        {
            MaterialProductionPerSecond[iterator] -= args.OldProductionRate[iterator];
            MaterialProductionPerSecond[iterator] += args.NewProductionRate[iterator];
        }
        return;
    }
    public void OverrideProductionRate(string[] type, int[] newAmount)
    {
        if (type.Length != newAmount.Length) return;
        for (int i = 0; i < type.Length; i++)
        {
            OverrideProductionRate(type[i], newAmount[i]);
        }
    }
    public void OverrideProductionRate(string type, int newAmount)
    {
        if (!MaterialProductionPerSecond.Keys.Contains(type)) MaterialProductionPerSecond.Add(type, 0);
        MaterialProductionPerSecond[type] = newAmount;
    }
    public void ChangeMaterialReciverOnTransitComp(EntityUid newDestination)
    {
        LinkedMaterialReciver = newDestination;
        return;
    }
    public void MaterialProductionTimeBased()
    {
        TimeSpan deltaTime = _timing.CurTime - LastUpdate;
        int deltaTimeInt = (int) deltaTime.TotalSeconds;
        if (deltaTimeInt <= 10) return;
        LastUpdate = _timing.CurTime;
        foreach (var iterator in MaterialProductionPerSecond.Keys)
        {
            if (!MaterialInTransit.ContainsKey(iterator))
            {
                MaterialInTransit.Add(iterator, 0);
            }
            MaterialInTransit[iterator] += (int) (MaterialProductionPerSecond[iterator] * deltaTimeInt);
            if (MaterialInTransit[iterator] > MaterialInTransitStorageCap) MaterialInTransit[iterator] = MaterialInTransitStorageCap;
        }
        //exit in case we dont have a linked reciver
        if (!TryComp<SignalSalvMaterialReciverStructureComponent>(LinkedMaterialReciver, out var reciverComp)) return;
        foreach (string iterator in MaterialInTransit.Keys)
        {
            if (MaterialInTransit[iterator] < reciverComp.MaterialCargoMin) continue;
            int unabletoAdd = _ingameServer.TryAddMaxPoints(LinkedMaterialReciver, iterator, reciverComp.MaterialCargoMin);
            MaterialInTransit[iterator] -= reciverComp.MaterialCargoMin + unabletoAdd;
        }
        return;
    }
    #region Command needs
    public bool ValidMaterial(String materialType)
    {
        foreach (var iterator in MaterialMiningRatesBase.BaseMaterials)
        {
            if (iterator.MaterialType == materialType) return true;
        }
        foreach (var iterator in MaterialMiningRatesBase.AdvancedMaterials)
        {
            if (iterator.MaterialType == materialType) return true;
        }
        foreach (var iterator in MaterialMiningRatesBase.SpecialMaterials)
        {
            if (iterator.MaterialType == materialType) return true;
        }
        return false;
    }
    #endregion
}
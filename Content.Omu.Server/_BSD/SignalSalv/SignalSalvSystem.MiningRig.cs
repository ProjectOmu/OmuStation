using Content.Omu.Shared._BSD.IngameConsoleSystem;

using Content.Omu.Server._BSD.SignalSalv.Components;
using Content.Omu.Server._BSD.SignalSalv.Events;
using Content.Omu.Server._BSD.SignalSalv.Helpers;

using Content.Omu.Server._BSD.MultiBlockSystem.Events;
using Content.Omu.Server._BSD.MultiBlockSystem.Components;

using System.Linq;

using Content.Shared.Materials;
using Content.Shared.Interaction;

using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;

namespace Content.Omu.Server._BSD.SignalSalv;

public sealed partial class BSDSignalSalvSystem : EntitySystem
{
    #region  User Interfacing
    public void IngameConsoleCommandSignalSalvMiningRig(Entity<SignalSalvMiningRigStructreComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        if (args.Type == IngameConsoleCommandType.ICC_START)
        {
            MiningRigRecalculation(ent, overRide: true);
            IngameConsoleHistoryChangeEvent ev = new("-> Machine started");
            RaiseLocalEvent(args.TerminalUid, ref ev);
            //Now add stuff to history to update that it worked;
        }
    }
    #endregion
    #region Mining Rig
    public void MiningRigRecalculationStructureChange(Entity<SignalSalvMiningRigStructreComponent> ent, ref MultiStructChangeEvent args)
    {
        if (!TryComp<MultiBlockStructureComponent>(ent, out var structureComp)) return;
        foreach (string providerType in ent.Comp.ProductivityTypes)
        {
            if (!structureComp.TypesPresent.ContainsKey(providerType)) continue;
            ent.Comp.ProductivityPoints += (int) structureComp.TypesPresent[providerType];
        }
        MiningRigRecalculation(ent);
        return;
    }
    public void OnAfterInteractOutpostData(Entity<SignalSalvOutpostDataComponent> ent, ref AfterInteractEvent args)
    {
        Random rand = new((int) _timing.CurTime.TotalSeconds);
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<MultiBlockPartComponent>(args.Target, out var multiStructPartComp))
            return;
        if (multiStructPartComp.ConstrollEntity == null) return;
        float deltaData = ent.Comp.OutpostData;
        if (ent.Comp.OutpostDataRandom)
        {
            deltaData = rand.NextFloat(ent.Comp.OutpostDataMin, ent.Comp.OutpostDataMax);
        }
        TryAddAdditonalRandomMaterialToPlanetMining((EntityUid) multiStructPartComp.ConstrollEntity);
        MiningRigRecalculationOutpostDataChange((EntityUid) multiStructPartComp.ConstrollEntity, deltaData);
        QueueDel(ent);
    }
    public bool TryAddAdditonalRandomMaterialToPlanetMining(EntityUid ent)
    {
        Random rand = new((int) _timing.CurTime.TotalSeconds);
        EntityUid mapUid = _mapSys.GetMapOrInvalid(Transform(ent).MapID);
        if (!TryComp<SignalSalvPlanetResourcesComponent>(mapUid, out var mapResourceComp)) return false;
        bool returnBool = false;
        int attempts;
        attempts = 10;
        while (attempts > 0)
        {
            if (mapResourceComp.AdvancedResourcePlanet)
            {
                //time to add a random special resource AND a advanced resource after -> advanced planets are good
                Material randomSpecialMat = MaterialMiningRatesBase.SpecialMaterials.ElementAt((int) rand.NextInt64(0, MaterialMiningRatesBase.SpecialMaterials.Count));
                if (!mapResourceComp.MiningRates.ContainsKey(randomSpecialMat.MaterialType))
                {
                    mapResourceComp.MiningRates.Add(randomSpecialMat.MaterialType, (int) rand.NextFloat(randomSpecialMat.MinResoucePerSecond, randomSpecialMat.MaxResoucePerSecond));
                    returnBool = true;
                    break;
                }
            }
            attempts--;
        }
        attempts = 10;
        while (attempts > 0)
        {
            Material randomAdvancedMat = MaterialMiningRatesBase.AdvancedMaterials.ElementAt((int) rand.NextInt64(0, MaterialMiningRatesBase.AdvancedMaterials.Count));
            if (!mapResourceComp.MiningRates.ContainsKey(randomAdvancedMat.MaterialType))
            {
                mapResourceComp.MiningRates.Add(randomAdvancedMat.MaterialType, (int) rand.NextFloat(randomAdvancedMat.MinResoucePerSecond, randomAdvancedMat.MaxResoucePerSecond));
                returnBool = true;
                break;
            }
            attempts--;
        }
        return returnBool;
    }
    public void MiningRigRecalculationOutpostDataChange(EntityUid ent, float deltaData)
    {
        if (!TryComp<SignalSalvMiningRigStructreComponent>(ent, out var comp)) return;
        comp.OutpostData += deltaData;
        Entity<SignalSalvMiningRigStructreComponent> passdownEnt = ent!;//we know this is not null
        MiningRigRecalculation(passdownEnt);
    }
    public void MiningRigRecalculation(Entity<SignalSalvMiningRigStructreComponent> ent, bool overRide = false)
    {
        float oldMiningRateModifier = ent.Comp.MiningRateModifier;
        ent.Comp.MiningRateModifier = 1;
        ent.Comp.MiningRateModifier += ent.Comp.GroundSurveyData;
        ent.Comp.MiningRateModifier += ent.Comp.OutpostData;
        ent.Comp.MiningRateModifier += (float) Math.Log(ent.Comp.ProductivityPoints, ent.Comp.ProductivityScalingBase);
        if (ent.Comp.MiningRateModifier == oldMiningRateModifier || overRide) return;
        Dictionary<ProtoId<MaterialPrototype>, int> oldMiningRates = new();
        Dictionary<ProtoId<MaterialPrototype>, int> newMiningRates = new();
        EntityUid mapUid = _mapSys.GetMapOrInvalid(Transform(ent).MapID);
        if (TryComp<SignalSalvPlanetResourcesComponent>(mapUid, out var mapResourceComp))
            foreach (var iterator in mapResourceComp.MiningRates.Keys)
            {
                oldMiningRates.Add(iterator, (int) (mapResourceComp.MiningRates[iterator] * oldMiningRateModifier));
                newMiningRates.Add(iterator, (int) (mapResourceComp.MiningRates[iterator] * ent.Comp.MiningRateModifier));
            }
        SignalSalvMiningRigProductionChangeEvent ev = new(oldMiningRates, newMiningRates);
        RaiseLocalEvent(ref ev);
    }
    #endregion
}
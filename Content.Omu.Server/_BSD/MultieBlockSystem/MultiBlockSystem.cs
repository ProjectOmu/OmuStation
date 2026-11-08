using System.Collections.Generic;

using Robust.Server.GameObjects;

using Robust.Shared.Random;
using Robust.Shared.Collections;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

using Content.Server.Construction;
using Content.Shared.Maps;
using Content.Server.Power.Components;

using Content.Omu.Server._BSD.MultiBlockSystem.Components;
using Content.Omu.Server._BSD.MultiBlockSystem.Events;
using Robust.Shared.Toolshed.Commands.Values;
using System.Linq;

namespace Content.Omu.Server._BSD.MultiBlockSystem;

public sealed partial class BSDMultiBlockSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    //magic number sets the override key to allow all connections
    private readonly ProtoId<MultiStructTypePrototype> _protoAll = "ALL";
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MultiBlockPartComponent, AfterConstructionChangeEntityEvent>(CheckIntegrity);
        SubscribeLocalEvent<MultiBlockPartComponent, AnchorStateChangedEvent>(CheckIntegrity);

        SubscribeLocalEvent<MultiBlockStructureCoreComponent, ComponentStartup>(StructureStart);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        PowerUpdateAll();
    }
    public void StructureStart(Entity<MultiBlockStructureCoreComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.StructureCoreProdID != null)
        {
            var structureCore = _entityManager.SpawnEntity(ent.Comp.StructureCoreProdID, Transform(ent).Coordinates);
            CheckIntegrity(structureCore);
        }
    }
    #region EnergyLogic
    private void PowerUpdateAll()
    {
        var machineQuerry = AllEntityQuery<MultiBlockStructureComponent, MultiBlockEnergyManagmentComponent>();
        while (machineQuerry.MoveNext(out var uidLoop, out var multiBlockStructureComp, out var multiBlockEnergyManagmentComp))
        {
            PowerUpdate(uidLoop, multiBlockStructureComp, multiBlockEnergyManagmentComp);
        }
    }
    private void PowerUpdate(EntityUid uid)
    {
        if (!TryComp<MultiBlockStructureComponent>(uid, out var multiBlockStructureComp)) return;
        if (!TryComp<MultiBlockEnergyManagmentComponent>(uid, out var multiBlockEnergyManagmentComp)) return;
        PowerUpdate(uid, multiBlockStructureComp, multiBlockEnergyManagmentComp);
        return;
    }
    private void PowerUpdate(EntityUid uid, MultiBlockStructureComponent comp, MultiBlockEnergyManagmentComponent powerComp)
    {
        if (powerComp.EnergyProvidingTypes == null) return;
        foreach (string providerType in powerComp.EnergyProvidingTypes)
        {
            if (!comp.EntityDic!.ContainsKey(providerType)) continue;
            foreach (Node iterator in comp.EntityDic[providerType])
            {
                // if (!TryComp<BatteryComponent>(iterator.Id, out var battery)) continue;
                // if (!TryComp<MultiBlockEnergyTransfairComponent>(iterator.Id, out var transfair)) continue;
                // float deltaChange = 0;
                // if (transfair.TransEnergy > 0) deltaChange = Math.Min(battery.LastCharge, transfair.TransEnergy * iterator.Efficency);
                // else deltaChange = Math.Max(battery.LastCharge - battery.MaxCharge, transfair.TransEnergy * iterator.Efficency);
                // ChangeChargeEvent ev = new ChangeChargeEvent(deltaChange);
                // RaiseLocalEvent(iterator.Id, ref ev, true);
                // powerComp.StoredEnergy = Math.Min(powerComp.StoredEnergy + deltaChange, powerComp.StoredEnergyCapacity);
            }
        }
        powerComp.StoredEnergy += powerComp.EnergyDelta;//structs own powergeneration/consumption
        if (powerComp.StoredEnergy < 0)
        {
            powerComp.StoredEnergy = 0;
            powerComp.Powered = false;
            return;
        }
        powerComp.Powered = true;
        return;
    }

    private void EnergyStroageUpdateAll()
    {
        var machineQuerry = AllEntityQuery<MultiBlockStructureComponent, MultiBlockEnergyManagmentComponent>();
        while (machineQuerry.MoveNext(out var uidLoop, out var multiBlockStructureComp, out var multiBlockEnergyManagmentComp))
        {
            EnergyStroageUpdate(uidLoop, multiBlockStructureComp, multiBlockEnergyManagmentComp);
        }
    }
    private void EnergyStroageUpdate(EntityUid uid)
    {
        if (!TryComp<MultiBlockStructureComponent>(uid, out var multiBlockStructureComp)) return;
        if (!TryComp<MultiBlockEnergyManagmentComponent>(uid, out var multiBlockEnergyManagmentComp)) return;
        EnergyStroageUpdate(uid, multiBlockStructureComp, multiBlockEnergyManagmentComp);
        return;
    }
    private void EnergyStroageUpdate(EntityUid uid, MultiBlockStructureComponent comp, MultiBlockEnergyManagmentComponent powerComp)
    {
        powerComp.StoredEnergyCapacity = 0;
        if (powerComp.EnergyCapacityTypes == null) return;
        foreach (string energyStorageType in powerComp.EnergyCapacityTypes)
        {
            if (!comp.EntityDic!.ContainsKey(energyStorageType)) continue;
            foreach (Node iterator in comp.EntityDic[energyStorageType])
            {
                if (!TryComp<MultiBlockEnergyStorageComponent>(iterator.Id, out var storageComp)) continue;
                powerComp.StoredEnergyCapacity += (int) (storageComp.StoreEnergy * iterator.Efficency);
            }
        }
        return;
    }
    #endregion

    #region Integrity
    private void CheckIntegrity(EntityUid uid, MultiBlockPartComponent comp, ref AfterConstructionChangeEntityEvent args)
    {
        if (comp.ConstrollEntity != null)
        {
            CheckIntegrity((EntityUid) comp.ConstrollEntity);
            return;
        }
        CheckIntegrityAll();//yea if ye are not part of a family we need to check everything TODO optimise this further
        return;
    }
    private void CheckIntegrity(EntityUid uid, MultiBlockPartComponent comp, ref AnchorStateChangedEvent args)
    {
        if (comp.ConstrollEntity != null)
        {
            CheckIntegrity((EntityUid) comp.ConstrollEntity);
            return;
        }
        CheckIntegrityAll();//yea if ye are not part of a family we need to check everything TODO optimise this further
        return;
    }

    private void CheckIntegrityAll()
    {
        ResetClaimedStatus();//not sure if this is even used anymore
        var machineQuerry = AllEntityQuery<MultiBlockStructureComponent, TransformComponent>();
        while (machineQuerry.MoveNext(out var uidLoop, out var multiBlockStructureComp, out var transComp))
        {
            if (multiBlockStructureComp.LinkedOriginPart == null) continue;
            if (!TryComp<MultiBlockPartComponent>(multiBlockStructureComp.LinkedOriginPart, out var multiblockPartComp)) continue;
            CheckIntegrity(uidLoop, multiBlockStructureComp, transComp, multiblockPartComp);
        }
        EnergyStroageUpdateAll();
        return;
    }
    public void CheckIntegrity(EntityUid controllUid)
    {
        if (!TryComp<MultiBlockStructureComponent>(controllUid, out var strucureComp)) return;
        if (strucureComp.LinkedOriginPart == null) return;
        if (!TryComp<MultiBlockPartComponent>(strucureComp.LinkedOriginPart, out var multiblockPartComp)) return;
        CheckIntegrity(controllUid, strucureComp, Transform((EntityUid) strucureComp.LinkedOriginPart), multiblockPartComp!);
    }
    public void CheckIntegrity(EntityUid uid, MultiBlockStructureComponent multiBlockStructureComp, TransformComponent transComp, MultiBlockPartComponent multiblockPartComp)
    {
        if (multiBlockStructureComp.LinkedOriginPart == null) return;
        bool onlySaveOne = true;
        List<Node> toSearchList = new List<Node>();
        HashSet<Node> foundSearchList = new HashSet<Node>();
        float minX = transComp.LocalPosition.X;
        float minY = transComp.LocalPosition.Y;
        float maxX = transComp.LocalPosition.X;
        float maxY = transComp.LocalPosition.Y;
        foreach (ProtoId<MultiStructTypePrototype> iterator in multiblockPartComp.PartTypes)
        {
            Node start = new Node();
            start.Id = (EntityUid) multiBlockStructureComp.LinkedOriginPart;
            start.Efficency = 1.0f;
            start.Type = iterator;
            foundSearchList.Add(start);
            if (onlySaveOne)
            {//sure the first node MAY have a million types but we ONLY NEED ONE in the search list
                toSearchList.Add(start);
                onlySaveOne = false;
            }
        }
        Node currentNode;
        do
        {
            //first get the most efficent item, then remove it from the to search list
            toSearchList.Sort((s1, s2) => s1.Efficency.CompareTo(s2.Efficency));
            currentNode = toSearchList.ElementAt(0);
            //then check the sides
            MultiBlockPartComponent targetComp = Comp<MultiBlockPartComponent>(currentNode.Id);
            targetComp.Claimed = true;
            for (int i = 0; i < 4; i++)
            {
                if (!targetComp.Connectability[i])
                {
                    continue;
                }
                Node temp = new Node();
                HashSet<ProtoId<MultiStructTypePrototype>> handDown = new();
                switch (i)
                {
                    case 0://N
                        handDown.UnionWith(targetComp.AllowedConnectionTypesNorth);
                        break;
                    case 1://E
                        handDown.UnionWith(targetComp.AllowedConnectionTypesEast);
                        break;
                    case 2://s
                        handDown.UnionWith(targetComp.AllowedConnectionTypesSouth);
                        break;
                    default://4; W
                        handDown.UnionWith(targetComp.AllowedConnectionTypesWest);
                        break;
                }
                temp.Id = CheckSide(currentNode.Id, i, handDown, multiBlockStructureComp.AllowedTypes, multiBlockStructureComp.PositionErrorMargine);
                if (!TryComp<MultiBlockPartComponent>(temp.Id, out var foundNodeComp))
                {
                    continue;//this should never fail but ye know somethimes it may just happen
                }
                temp.Efficency = currentNode.Efficency * foundNodeComp.TransmissionEfficency;
                foreach (ProtoId<MultiStructTypePrototype> iterator in foundNodeComp.PartTypes)
                {
                    temp.Type = iterator;
                    if (temp.Id != currentNode.Id)//this means there is no entity found but cant use null(and every EUID is unique so... yea)
                    {
                        var foundTransComp = Transform(temp.Id);
                        minX = Math.Min(minX, foundTransComp.LocalPosition.X);
                        minY = Math.Min(minY, foundTransComp.LocalPosition.Y);
                        maxX = Math.Max(maxX, foundTransComp.LocalPosition.X);
                        maxY = Math.Max(maxY, foundTransComp.LocalPosition.Y);
                        if (!foundSearchList.Contains(temp))//sadly only now can we test if this node already exists in the hashset
                        {
                            toSearchList.Add(temp.Clone());
                            foundSearchList.Add(temp.Clone());
                        }
                    }
                }
            }
            toSearchList.Remove(currentNode);
        } while (toSearchList.Count > 0);
        //update the actual values to the master structure and link them all
        multiBlockStructureComp.TypePresence2DMapDimentionX = (int) Math.Abs(maxX - minX);
        multiBlockStructureComp.TypePresence2DMapDimentionY = (int) Math.Abs(maxY - minY);
        Dictionary<string, List<Node>> newEntityDict = new Dictionary<string, List<Node>>();
        multiBlockStructureComp.TypesPresent = new Dictionary<string, float>();
        multiBlockStructureComp.TypePresence2DMap = new Dictionary<string, bool?[,]>();
        foreach (Node addNode in foundSearchList)
        {
            int gridPosX = (int) (Transform(addNode.Id).LocalPosition.X - minX);
            int gridPosY = (int) (Transform(addNode.Id).LocalPosition.Y - minY);
            addNode.LocRelativeGRid.X = gridPosX;
            addNode.LocRelativeGRid.Y = gridPosY;
            if (newEntityDict.ContainsKey(addNode.Type))
            {
                newEntityDict[addNode.Type].Add(addNode.Clone());
            }
            else
            {
                List<Node> newList = new List<Node>();
                newList.Add(addNode.Clone());
                newEntityDict.Add(addNode.Type, newList);
            }
            if (multiBlockStructureComp.TypesPresent.ContainsKey(addNode.Type))
            {
                multiBlockStructureComp.TypesPresent[addNode.Type] += addNode.Efficency * Comp<MultiBlockPartComponent>(addNode.Id).MachinePower;
            }
            else
            {
                multiBlockStructureComp.TypesPresent.Add(addNode.Type, addNode.Efficency * Comp<MultiBlockPartComponent>(addNode.Id).MachinePower);
            }
            if (multiBlockStructureComp.TypePresence2DMap.ContainsKey(addNode.Type) == false)
            {
                multiBlockStructureComp.TypePresence2DMap.Add(addNode.Type, new bool?[multiBlockStructureComp.TypePresence2DMapDimentionY + 1, multiBlockStructureComp.TypePresence2DMapDimentionX + 1]);
                for (int genIterator1 = 0; genIterator1 < multiBlockStructureComp.TypePresence2DMapDimentionY; genIterator1++)
                {
                    for (int genIterator2 = 0; genIterator2 < multiBlockStructureComp.TypePresence2DMapDimentionX; genIterator2++)
                    {
                        multiBlockStructureComp.TypePresence2DMap[addNode.Type][genIterator1, genIterator2] = false;
                    }
                }
            }
            multiBlockStructureComp.TypePresence2DMap[addNode.Type][gridPosY, gridPosX] = true;
        }
        var ev = new MultiStructChangeEvent(GetChangeInEntityDict(newEntityDict, multiBlockStructureComp.EntityDic));//let subsys know things happened
        RaiseLocalEvent(uid, ref ev);
    }
    private Dictionary<string, List<Node>> GetChangeInEntityDict(Dictionary<string, List<Node>> newDict, Dictionary<string, List<Node>>? oldDict)
    {
        if (oldDict == null) return new();
        bool noChange = true;
        if (newDict.Keys.Count == oldDict.Keys.Count)
        {
            foreach (var iterator in newDict.Keys)
            {
                if (!oldDict.ContainsKey(iterator))
                {
                    noChange = false;
                    continue;
                }
                if (oldDict[iterator].Count != newDict[iterator].Count)
                {
                    noChange = false;
                    continue;
                }
            }
        }
        if (noChange)
        {
            return new();
        }
        Dictionary<string, List<Node>> returnValue = new();
        foreach (var iterator in oldDict.Keys)
        {
            if (newDict.ContainsKey(iterator))
            {
                if (newDict[iterator].Count == oldDict[iterator].Count) continue;
                foreach (var iterator2 in oldDict[iterator])
                {
                    if (newDict[iterator].Contains(iterator2)) continue;
                    if (!returnValue.ContainsKey(iterator)) returnValue.Add(iterator, new());
                    returnValue[iterator].Add(iterator2);
                }
            }
            else
            {
                returnValue.Add(iterator, oldDict[iterator]);
            }
        }
        return returnValue;
    }
    private void ResetClaimedStatus()
    {
        var resetWaveEntites = AllEntityQuery<MultiBlockPartComponent>();
        while (resetWaveEntites.MoveNext(out var uidLoop, out var multiblockPartComp))
        {
            multiblockPartComp.Claimed = false;
            multiblockPartComp.ConstrollEntity = null;
        }
        return;
    }
    private EntityUid CheckSide(EntityUid uid, int sideNum, HashSet<ProtoId<MultiStructTypePrototype>> allowedTypes, HashSet<ProtoId<MultiStructTypePrototype>> structureTypesAllowed, float margineOfError)
    {
        Vector2d targetCordVec = new();
        Angle rotation = Transform(uid).LocalRotation;
        float rotationOffset = (float) rotation / ((float) Math.PI * 1.0f / 2.0f);
        sideNum -= (int) rotationOffset;
        if (sideNum < 0)
        {
            sideNum += 4;
        }
        targetCordVec.X = Transform(uid).Coordinates.Position.X;
        targetCordVec.Y = Transform(uid).Coordinates.Position.Y;
        switch (sideNum)
        {
            case 0://N
                targetCordVec.Y += 1.0f;
                break;
            case 1://E
                targetCordVec.X += 1.0f;
                break;
            case 2://s
                targetCordVec.Y -= 1.0f;
                break;
            default://3; W
                targetCordVec.X -= 1.0f;
                break;
        }
        //get the entity on that cordinate
        var foundEntities = AllEntityQuery<TransformComponent, MultiBlockPartComponent>();
        while (foundEntities.MoveNext(out var uidLoop, out var transComp, out var multiblockPartComp))
        {
            if (multiblockPartComp.Claimed)//ignore already in use parts
            {
                continue;
            }
            if (!transComp.Anchored) continue;
            if (multiblockPartComp.PartTypes == null) continue;
            bool allowedPart = false;
            foreach (ProtoId<MultiStructTypePrototype> iterator in multiblockPartComp.PartTypes)
            {
                if (!structureTypesAllowed.Contains(iterator)) continue;
                if (allowedTypes.Contains(_protoAll)) allowedPart = true;//override condition allow any type
                if (!allowedTypes.Contains(iterator)) continue;
                allowedPart = true;
            }
            if (!allowedPart) continue;
            if (Transform(uid).GridUid == null || Transform(uidLoop).GridUid == null || Transform(uid).GridUid!.Value != Transform(uidLoop).GridUid!.Value)//same grid check
            {
                continue;
            }
            Vector2d checkCordVec = new();
            checkCordVec.X = Transform(uidLoop).Coordinates.Position.X;
            checkCordVec.Y = Transform(uidLoop).Coordinates.Position.Y;
            if (Math.Abs(checkCordVec.X - targetCordVec.X) > margineOfError)
            {
                continue;
            }
            if (Math.Abs(checkCordVec.Y - targetCordVec.Y) > margineOfError)
            {
                continue;
            }
            return uidLoop;
        }
        return uid;
    }
    #endregion
}

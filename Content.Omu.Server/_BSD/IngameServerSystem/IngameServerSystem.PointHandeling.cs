using Content.Omu.Server._BSD.IngameServerSystem.Helpers;
using Content.Omu.Server._BSD.IngameServerSystem.Components;

using Content.Omu.Server._BSD.MultiBlockSystem.Events;

using System.Linq;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;
using System.Drawing;
using Content.Omu.Server._BSD.MultiBlockSystem.Components;

namespace Content.Omu.Server._BSD.IngameServerSystem
{
    public sealed partial class BSDIngameServerSystem : EntitySystem
    {
        public void OnMultistructUpdatePointCapacity(Entity<IngameServerPointCapacityComponent> ent, ref MultiStructChangeEvent args)
        {
            if (ent.Comp.MultistructScaling == false) return;
            if (!TryComp<MultiBlockStructureComponent>(ent, out var compStruct)) return;
            float constructPower = 0f;
            foreach (var constructPowerProvider in ent.Comp.CapacityProvidingTypes)
            {
                if (!compStruct.TypesPresent.ContainsKey(constructPowerProvider)) continue;
                constructPower += compStruct.TypesPresent[constructPowerProvider];
            }
            foreach (var pointType in ent.Comp.CapacityExpansion.Keys)
            {
                ent.Comp.CapacityExpansion[pointType] = 0;
                if (ent.Comp.CapacityBaseline.ContainsKey(pointType))
                {
                    ent.Comp.CapacityExpansion[pointType] += ent.Comp.CapacityBaseline[pointType];
                }
                if (ent.Comp.CapacityLinearGrowth.ContainsKey(pointType))
                {
                    ent.Comp.CapacityExpansion[pointType] += (int) (ent.Comp.CapacityLinearGrowth[pointType] * constructPower);
                }
                if (ent.Comp.CapacityQuadraticGrowth.ContainsKey(pointType))
                {
                    ent.Comp.CapacityExpansion[pointType] += (int) (ent.Comp.CapacityQuadraticGrowth[pointType] * constructPower * constructPower);
                }
            }
            //TODO: trigger a structure wide point reevaluation!!!
            //(not needed rn as any multistruct change rechecks EVERY other multistruct but for the future this is a note)
        }

        /// <summary>
        /// returns (int) 0 in case there is no IngameServerComponent on the given entity
        /// </summary>
        /// <param name="uid">Entity UID of the Server in question</param>
        /// <param name="type">Type of point you are trying to add</param>
        /// <returns></returns>
        public int GetMaxPointAddition(EntityUid uid, string type)
        {
            if (!TryComp<IngameServerComponent>(uid, out var serverComp))
                return 0;
            return GetMaxPointAddition(serverComp, type);
        }

        public int GetMaxPointAddition(IngameServerComponent serverComp, string type)
        {
            if (!serverComp.StoredPointsCapacity.ContainsKey(type) || serverComp.StoredPointsCapacity[type] == null)
                return int.MaxValue;
            if (!serverComp.StoredPoints.ContainsKey(type))
                serverComp.StoredPoints.Add(type, 0);//ensure we have the type present we want to check for better safe than sorry
            return (int) serverComp.StoredPointsCapacity[type]! - serverComp.StoredPoints[type];
        }
        /// <summary>
        /// Tries to add the maximum amount of points possible returns the amount of points it was unable to add
        /// </summary>
        /// <param name="serverComp"></param>
        /// <param name="type"></param>
        /// <param name="deltaAdd"></param>
        /// <returns></returns>
        public int TryAddMaxPoints(IngameServerComponent serverComp, string type, int deltaAdd)
        {
            int toAdd = Math.Min(deltaAdd, GetMaxPointAddition(serverComp, type));
            if (!serverComp.StoredPoints.ContainsKey(type))
            {
                serverComp.StoredPoints.Add(type, 0);
            }
            serverComp.StoredPoints[type] += toAdd;
            return deltaAdd - toAdd;
        }
        public int TryAddMaxPoints(EntityUid entityUid, string type, int deltaAdd)
        {
            if (!TryComp<IngameServerComponent>(entityUid, out var comp))
                return deltaAdd;
            return TryAddMaxPoints(comp, type, deltaAdd);
        }
        /// <summary>
        /// Get the total points of a point type, used the ServerClientLinkSystem for this
        /// </summary>
        /// <param name="pointType">Type of point stored in the ServerComponent</param>
        /// <param name="connectionType">Connection type of the serverclientlink</param>
        /// <param name="querryType"></param>
        /// <returns></returns>
        public bool TryGetPointsTotalNetwork(EntityUid uid, string pointType, string connectionType, QuerryTypes querryType,
                                            out int pointsPresent)
        {
            pointsPresent = 0;
            switch (querryType)
            {
                case QuerryTypes.local:
                    return TryGetLocalPoints(uid, pointType, out pointsPresent);
                case QuerryTypes.networkTotal:
                    if (!_clientLinkSystem.TryGetNetworkTotal(uid, connectionType, out var network)) return false;
                    GetNetworkPointsTotal(network, pointType, out pointsPresent);
                    return true;
                case QuerryTypes.networkDownwards:
                    if (!_clientLinkSystem.TryGetNetworkDownwards(uid, connectionType, out var networkDown)) return false;
                    GetNetworkPointsTotal(networkDown, pointType, out pointsPresent);
                    return true;
                case QuerryTypes.networkUpwards:
                    if (!_clientLinkSystem.TryGetNetworkUpwards(uid, connectionType, out var networkUp)) return false;
                    GetNetworkPointsTotal(networkUp, pointType, out pointsPresent);
                    return true;
                default:
                    break;
            }
            return false;
        }
        private bool TryGetLocalPoints(EntityUid uid, string type, out int pointsPresent)
        {
            pointsPresent = 0;
            if (!TryComp<IngameServerComponent>(uid, out var serverComp)) return false;
            if (!serverComp.StoredPoints.ContainsKey(type)) return false;
            pointsPresent = serverComp.StoredPoints[type];
            return true;
        }
        private void GetNetworkPointsTotal(HashSet<EntityUid> uidHash, string type, out int pointsPresent)
        {
            pointsPresent = 0;
            foreach (var iterator in uidHash)
            {
                if (!TryComp<IngameServerComponent>(iterator, out var iteratorComp)) continue;
                if (!iteratorComp.StoredPoints.ContainsKey(type)) continue;
                pointsPresent += iteratorComp.StoredPoints[type];
            }
        }
    }
}
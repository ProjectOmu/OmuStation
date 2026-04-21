using System.Linq;
using Content.Omu.Server.GameDirector.Metric.Components;
using Content.Server.Station.Systems;
using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Content.Goobstation.Common.StationEvent.Metrics;
namespace Content.Omu.Server.GameDirector.Metric;

/// <summary>
///   Measures the strength of friendies and hostiles. Also calculates related health / death stats.
///
///   I've used 10 points per entity because later we might somehow estimate combat strength
///   as a multiplier. We could for instance detect damage delt / recieved and look also at
///   entity hitpoints & resistances as an analogue for danger.
///
///   Writes the following
///   Friend : -10 per each friendly entity on the station (negative is GOOD in chaos)
///   Hostile : about 10 points per hostile (those with antag roles) - varies per constants
///   Combat: friendlies + hostiles (to represent the balance of power)
///   Death: 20 per dead body,
///   Medical: 10 for crit + 0.05 * damage (so 5 for 100 damage),
/// </summary>
public sealed class CombatMetricSystem : ChaosMetricSystem<CombatMetricComponent>
{
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    private double InventoryPower(EntityUid uid, CombatMetricComponent component)
    {
        // scan every item in hands and inventory and sum up threat tags (e.g. guns, melee weapons)
        // this gives a rough estimate of how dangerous this entity is in a fight
        var tagsQ = GetEntityQuery<TagComponent>();
        var allTags = new HashSet<ProtoId<TagPrototype>>();

        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
            if (tagsQ.TryGetComponent(item, out var tags))
                allTags.UnionWith(tags.Tags);

        var threat = allTags.Sum(key => component.ItemThreat.GetValueOrDefault(key));

        // cap at MaxItemThreat so one super-geared entity does not dominate the whole metric
        return threat > component.MaxItemThreat ? component.MaxItemThreat : threat;
    }

    protected override ChaosMetrics CalculateChaos(EntityUid metricUid,
        CombatMetricComponent combatMetric,
        CalculateChaosEvent args)
    {
        var query = EntityQueryEnumerator<MobStateComponent, DamageableComponent, TransformComponent>();
        var mindQuery = GetEntityQuery<MindContainerComponent>();
        var npcFacQ = GetEntityQuery<NpcFactionMemberComponent>();
        var powerQ = GetEntityQuery<CombatPowerComponent>();
        double hostilesChaos = 0;
        double friendliesChaos = 0;
        double medicalChaos = 0;
        double deathChaos = 0;

        var stationGrids = _stationSystem.GoobGetAllStationGrids();
        while (query.MoveNext(out var uid, out var mobState, out var damage, out var transform))
        {
            if (transform.GridUid == null || !stationGrids.Contains(transform.GridUid.Value))
                continue;

            var isAntag = false;

            if (mindQuery.TryGetComponent(uid, out var mind) && mind.Mind != null)
                isAntag = _roles.MindIsAntagonist(mind.Mind);
            else
            {
                if (!CalculateNPC(npcFacQ, uid, powerQ, combatMetric, ref isAntag))
                    continue;
            }

            powerQ.TryGetComponent(uid, out var power);
            var threatMultiple = power?.Threat ?? 1.0f;

            if (isAntag)
            {
                if (mobState.CurrentState != MobState.Alive)
                {
                    deathChaos += combatMetric.DeadScore;
                    continue;
                }
            }
            else
            {
                if (!CalculateFriendlyCount(combatMetric, mobState, damage, ref deathChaos, ref medicalChaos))
                    continue;
            }

            var entityThreat = InventoryPower(uid, combatMetric);

            if (isAntag)
                hostilesChaos += (entityThreat + combatMetric.HostileScore) * threatMultiple;
            else
                friendliesChaos += (entityThreat + combatMetric.FriendlyScore) * threatMultiple;
        }

        var chaos = new ChaosMetrics(new Dictionary<ChaosMetric, double>()
        {
            {ChaosMetric.Friend, -friendliesChaos}, // Friendlies are good, so make a negative chaos score
            {ChaosMetric.Hostile, hostilesChaos},

            {ChaosMetric.Death, deathChaos},
            {ChaosMetric.Medical, medicalChaos},
        });
        return chaos;
    }

    private bool CalculateNPC(
        EntityQuery<NpcFactionMemberComponent> npcFacQ,
        EntityUid uid,
        EntityQuery<CombatPowerComponent> powerQ,
        CombatMetricComponent combatMetric,
        ref bool isAntag)
    {
        // skip entities with no faction (non-combat mobs like butterflies)
        if (!npcFacQ.TryGetComponent(uid, out var fac))
            return true;

        var hasPower = powerQ.HasComponent(uid);
        // hostile to NanoTrasen faction = counts as an enemy
        var isHostile = _faction.IsFactionHostile(combatMetric.FriendlyFactionId, (uid, fac));

        if (isHostile)
        {
            isAntag = true;
            // if the npc has a CombatPower override we let that handle the threat multiplier, skip normal scoring
            return !hasPower;
        }

        if (!hasPower)
            return true;

        // friendly npc with a CombatPower override - count as a friendly with the override multiplier
        isAntag = false;
        return false;
    }

    private static bool CalculateFriendlyCount(CombatMetricComponent combatMetric,
        MobStateComponent mobState,
        DamageableComponent damage,
        ref double deathChaos,
        ref double medicalChaos)
    {
        if (mobState.CurrentState == MobState.Dead)
        {
            deathChaos += combatMetric.DeadScore;
            return false;
        }
        var totalDamage = damage.Damage.GetTotal().Double();
        medicalChaos += totalDamage * combatMetric.MedicalMultiplier;
        if (mobState.CurrentState == MobState.Critical)
            medicalChaos += combatMetric.CritScore;

        return true;
    }
}

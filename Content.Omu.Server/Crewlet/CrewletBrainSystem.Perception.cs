// SPDX-License-Identifier: MIT

using Content.Server.Nutrition.Components;
using Content.Shared.Nutrition.Components;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private const int PickAttempts = 3;

    private readonly HashSet<EntityUid> _scan = new();
    private readonly HashSet<EntityUid> _near = new();

    private enum CrewletPick : byte
    {
        Sleeper,
        Animal,
        Loot,
        Table,
        Window,
        Seat,
        Horn,
        Puddle,
        Food,
        Anomaly,
        Hat,
        Playmate,
    }

    private void Perceive(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        brain.Scanned = now >= brain.NextScan;
        if (!brain.Scanned)
            return;

        brain.NextScan = now + brain.ScanInterval;
        brain.People.Clear();
        brain.Sleepers.Clear();
        brain.Animals.Clear();
        brain.Items.Clear();
        brain.Tables.Clear();
        brain.Windows.Clear();
        brain.Seats.Clear();
        brain.Puddles.Clear();
        brain.Edibles.Clear();
        brain.Crewlets.Clear();
        brain.Anomalies.Clear();

        foreach (var (uid, ready) in brain.TargetReadyAt)
        {
            if (ready <= now || TerminatingOrDeleted(uid))
                brain.TargetReadyAt.Remove(uid);
        }

        _scan.Clear();
        _lookup.GetEntitiesInRange(ent.Owner,
            brain.VisionRadius,
            _scan,
            LookupFlags.Uncontained | LookupFlags.Approximate);

        foreach (var uid in _scan)
        {
            Sort(brain, uid);
        }

        _scan.Clear();
        NoticeFriends(ent, now);
    }

    private void NoticeFriends(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        foreach (var (uid, ready) in brain.HelloReadyAt)
        {
            if (ready <= now || TerminatingOrDeleted(uid))
                brain.HelloReadyAt.Remove(uid);
        }

        _near.Clear();
        foreach (var person in brain.People)
        {
            if (DistanceTo(ent, person) <= brain.HelloLeaveRange)
                _near.Add(person);
        }

        foreach (var crewlet in brain.Crewlets)
        {
            if (crewlet != ent.Owner && DistanceTo(ent, crewlet) <= brain.HelloLeaveRange)
                _near.Add(crewlet);
        }

        foreach (var person in _near)
        {
            if (!brain.Nearby.Contains(person))
                brain.Arrivals.Add(person);
        }

        brain.Arrivals.IntersectWith(_near);
        brain.Nearby.Clear();
        brain.Nearby.UnionWith(_near);
        _near.Clear();

        TryComp<CrewletReputationComponent>(ent, out var reputation);
        foreach (var person in brain.Nearby)
        {
            if (!brain.Arrivals.Contains(person) || DistanceTo(ent, person) > brain.HelloRange)
                continue;

            var welcome = Welcomes(ent, reputation, person, now);
            if (welcome && brain.HelloCandidate != null)
                continue;

            brain.Arrivals.Remove(person);
            if (welcome)
                brain.HelloCandidate = person;
        }
    }

    private bool Welcomes(Entity<CrewletBrainComponent> ent,
        CrewletReputationComponent? reputation,
        EntityUid person,
        TimeSpan now)
    {
        if (!_mobState.IsAlive(person))
            return false;

        if (_crewletQuery.HasComp(person))
            return now >= ent.Comp.CrewletHelloReadyAt;

        return reputation != null
            && !ent.Comp.HelloReadyAt.ContainsKey(person)
            && _reputation.GetOpinion((ent.Owner, reputation), person) >= reputation.FriendlyThreshold;
    }

    private void Sort(CrewletBrainComponent brain, EntityUid uid)
    {
        if (_mobStateQuery.HasComp(uid))
        {
            if (_crewletQuery.HasComp(uid))
                brain.Crewlets.Add(uid);
            else if (_humanoidQuery.HasComp(uid))
                brain.People.Add(uid);
            else if (_popupQuery.HasComp(uid))
                brain.Animals.Add(uid);

            if (_sleepingQuery.HasComp(uid))
                brain.Sleepers.Add(uid);

            return;
        }

        if (_itemQuery.HasComp(uid) && !Transform(uid).Anchored)
            brain.Items.Add(uid);

        if (_edibleQuery.HasComp(uid))
            brain.Edibles.Add(uid);

        if (_climbableQuery.HasComp(uid))
            brain.Tables.Add(uid);

        if (_strapQuery.HasComp(uid))
            brain.Seats.Add(uid);

        if (_puddleQuery.HasComp(uid))
            brain.Puddles.Add(uid);

        if (_wallMountQuery.HasComp(uid) && _popupQuery.HasComp(uid))
            brain.Windows.Add(uid);

        if (_anomalyQuery.TryComp(uid, out var anomaly) && anomaly.CorePrototype == brain.MergeCore)
            brain.Anomalies.Add(uid);
    }

    private EntityUid? PickNearest(Entity<CrewletBrainComponent> ent,
        List<EntityUid> candidates,
        CrewletPick pick,
        TimeSpan now,
        float? range = null)
    {
        var xform = Transform(ent);
        var origin = _transform.GetWorldPosition(xform);

        for (var attempt = 0; attempt < PickAttempts; attempt++)
        {
            EntityUid? best = null;
            var bestDistance = range ?? ent.Comp.VisionRadius;

            foreach (var uid in candidates)
            {
                if (!IsCandidate(ent, uid, pick, now))
                    continue;

                var other = Transform(uid);
                if (other.MapID != xform.MapID)
                    continue;

                var distance = (_transform.GetWorldPosition(other) - origin).Length();
                if (distance > bestDistance)
                    continue;

                best = uid;
                bestDistance = distance;
            }

            if (best is not { } chosen)
                return null;

            if (InSight(ent, chosen))
                return chosen;

            Cooldown(ent, chosen, ent.Comp.FailureCooldown, now);
        }

        return null;
    }

    private EntityUid? PickPerson(Entity<CrewletBrainComponent> ent, bool fond, TimeSpan now)
    {
        TryComp<CrewletReputationComponent>(ent, out var reputation);

        var total = 0f;
        foreach (var uid in ent.Comp.People)
        {
            total += PersonWeight(ent, reputation, uid, fond, now);
        }

        if (total <= 0f)
            return null;

        var roll = _random.NextFloat() * total;
        EntityUid? chosen = null;
        foreach (var uid in ent.Comp.People)
        {
            var weight = PersonWeight(ent, reputation, uid, fond, now);
            if (weight <= 0f)
                continue;

            chosen = uid;
            roll -= weight;
            if (roll <= 0f)
                break;
        }

        if (chosen is not { } person)
            return null;

        if (InSight(ent, person))
            return person;

        Cooldown(ent, person, ent.Comp.FailureCooldown, now);
        return null;
    }

    private float PersonWeight(Entity<CrewletBrainComponent> ent,
        CrewletReputationComponent? reputation,
        EntityUid person,
        bool fond,
        TimeSpan now)
    {
        if (!AliveNearby(ent, person))
            return 0f;

        if (reputation == null)
            return fond && TargetReady(ent.Comp, person, now) ? 1f : 0f;

        var opinion = _reputation.GetOpinion((ent.Owner, reputation), person);
        if (fond)
        {
            if (!TargetReady(ent.Comp, person, now) || opinion <= reputation.WaryThreshold)
                return 0f;

            return 1f + MathF.Max(opinion, 0f) / MathF.Max(reputation.FriendlyThreshold, 1f);
        }

        if (opinion > reputation.WaryThreshold)
            return 0f;

        return 1f + (reputation.WaryThreshold - opinion) / MathF.Max(MathF.Abs(reputation.WaryThreshold), 1f);
    }

    private bool IsCandidate(Entity<CrewletBrainComponent> ent, EntityUid uid, CrewletPick pick, TimeSpan now)
    {
        if (TerminatingOrDeleted(uid)
            || !TargetReady(ent.Comp, uid, now)
            || _container.IsEntityInContainer(uid))
            return false;

        return pick switch
        {
            CrewletPick.Sleeper => _sleepingQuery.HasComp(uid) && _mobState.IsAlive(uid),
            CrewletPick.Animal => _mobState.IsAlive(uid),
            CrewletPick.Loot => !_whitelist.IsWhitelistPass(ent.Comp.LootBlacklist, uid),
            CrewletPick.Seat => !_whitelist.IsWhitelistPass(ent.Comp.SeatBlacklist, uid),
            CrewletPick.Horn => _hornQuery.HasComp(uid) && !_whitelist.IsWhitelistPass(ent.Comp.HornBlacklist, uid),
            CrewletPick.Puddle => CanMop(uid),
            CrewletPick.Food => CanEat(ent, uid),
            CrewletPick.Anomaly => _merge.CanMerge(uid),
            CrewletPick.Hat => !_whitelist.IsWhitelistPass(ent.Comp.LootBlacklist, uid)
                && Fits(ent, uid, ent.Comp.HatSlot),
            CrewletPick.Playmate => uid != ent.Owner
                && _mobState.IsAlive(uid)
                && TryComp<CrewletBrainComponent>(uid, out var other)
                && IdleTier(other),
            _ => true,
        };
    }

    private bool CanMop(EntityUid uid)
    {
        return _puddleQuery.TryComp(uid, out var puddle)
            && _solutions.TryGetSolution(uid, puddle.SolutionName, out _, out var solution)
            && !_puddle.CanFullyEvaporate(solution);
    }

    private bool CanEat(Entity<CrewletBrainComponent> ent, EntityUid food)
    {
        if (_mobStateQuery.HasComp(food) || !_ingestion.CanConsume(ent, food))
            return false;

        if (IsHungry(ent) && !HasComp<BadFoodComponent>(food) && _ingestion.TotalNutrition(food, ent) > 0f)
            return true;

        return IsThirsty(ent) && !HasComp<BadDrinkComponent>(food) && _ingestion.TotalHydration(food, ent) > 1f;
    }

    private bool IsHungry(Entity<CrewletBrainComponent> ent)
    {
        return TryComp<HungerComponent>(ent, out var hunger) && hunger.CurrentThreshold <= ent.Comp.HungerThreshold;
    }

    private bool IsThirsty(Entity<CrewletBrainComponent> ent)
    {
        return TryComp<ThirstComponent>(ent, out var thirst)
            && thirst.CurrentThirstThreshold <= ent.Comp.ThirstThreshold;
    }

    private bool AliveNearby(Entity<CrewletBrainComponent> ent, EntityUid uid)
    {
        return !TerminatingOrDeleted(uid)
            && _mobState.IsAlive(uid)
            && !_container.IsEntityInContainer(uid)
            && DistanceTo(ent, uid) <= ent.Comp.VisionRadius;
    }

    private bool InSight(Entity<CrewletBrainComponent> ent, EntityUid target)
    {
        return _examine.InRangeUnOccluded(ent, target, ent.Comp.VisionRadius + 0.5f);
    }

    private bool TargetReady(CrewletBrainComponent brain, EntityUid target, TimeSpan now)
    {
        return !brain.TargetReadyAt.TryGetValue(target, out var ready) || now >= ready;
    }

    private void Cooldown(Entity<CrewletBrainComponent> ent, EntityUid target, TimeSpan duration, TimeSpan now)
    {
        ent.Comp.TargetReadyAt[target] = now + duration;
    }
}

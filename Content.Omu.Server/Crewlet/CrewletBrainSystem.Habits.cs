// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Access.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.VendingMachines;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private void OnOwnerInteractHand(Entity<CrewletOwnerComponent> ent, ref UserInteractHandEvent args)
    {
        Witness(ent, CrewletToolAction.Hand, null, args.Target);
    }

    private void OnOwnerInteractUsing(Entity<CrewletOwnerComponent> ent, ref UserInteractUsingEvent args)
    {
        Witness(ent, CrewletToolAction.Use, args.Used, args.Target);
    }

    private void OnActivateInWorld(ActivateInWorldEvent args)
    {
        if (_ownerQuery.TryComp(args.User, out var owner))
            Witness((args.User, owner), CrewletToolAction.Activate, null, args.Target);
    }

    private void OnOwnerVerb(ExecuteVerbEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } user
            || !_ownerQuery.TryComp(user, out var owner)
            || !TryGetEntity(args.Target, out var target)
            || target == user
            || !Watched((user, owner))
            || !_verbs.GetLocalVerbs(target.Value, user, args.RequestedVerb.GetType())
                .TryGetValue(args.RequestedVerb, out var verb)
            || verb.Disabled)
            return;

        var tool = verb is UtilityVerb ? _hands.GetActiveItem(user) : null;
        Witness((user, owner), CrewletToolAction.Verb, tool, target.Value, verb);
    }

    private bool OnOwnerAltUse(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (session?.AttachedEntity is { } user
            && uid != user
            && _ownerQuery.TryComp(user, out var owner)
            && Exists(uid)
            && Watched((user, owner))
            && _verbs.GetLocalVerbs(uid, user, typeof(AlternativeVerb)).Min is { Disabled: false } verb)
            Witness((user, owner), CrewletToolAction.Verb, null, uid, verb);

        return false;
    }

    private bool Watched(Entity<CrewletOwnerComponent> owner)
    {
        foreach (var uid in owner.Comp.Crewlets)
        {
            if (TryComp<CrewletBrainComponent>(uid, out var brain) && brain.Active && InSight((uid, brain), owner))
                return true;
        }

        return false;
    }

    private void OnOwnerContact(Entity<CrewletOwnerComponent> ent, ref ContactInteractionEvent args)
    {
        var tick = _timing.CurTick;
        foreach (var uid in ent.Comp.Crewlets)
        {
            if (!TryComp<CrewletBrainComponent>(uid, out var brain))
                continue;

            var watches = brain.Watches;
            for (var i = 0; i < watches.Count; i++)
            {
                var watch = watches[i];
                if (watch.State != CrewletWatchState.Seen || watch.Tick != tick || watch.Target != args.Other)
                    continue;

                watch.Handled = true;
                watches[i] = watch;
            }
        }
    }

    private void Witness(Entity<CrewletOwnerComponent> owner,
        CrewletToolAction action,
        EntityUid? tool,
        EntityUid target,
        Verb? verb = null)
    {
        if (_crewletQuery.HasComp(owner) || PrototypeOf(target) is not { } prototype)
            return;

        var verbType = verb?.GetType().Name;
        var verbName = verb == null ? null : VerbName(verb);
        var now = _timing.CurTime;
        var tick = _timing.CurTick;
        foreach (var uid in owner.Comp.Crewlets)
        {
            if (!TryComp<CrewletBrainComponent>(uid, out var brain) || !brain.Active)
                continue;

            var ent = new Entity<CrewletBrainComponent>(uid, brain);
            if (!InSight(ent, owner))
            {
                Retract(brain, tick, target);
                continue;
            }

            if (action == CrewletToolAction.Use && tool is { } used)
                Teach(brain, used, target);

            if (!TryHabit(ent, action, tool, target, verbType, verbName, out var key))
            {
                Retract(brain, tick, target);
                continue;
            }

            var expendable = verb != null || _whitelist.IsWhitelistPass(brain.ExpendableTargets, target);
            var risky = verb != null && verb.Impact >= brain.RiskyImpact;
            var facts = Facts(ent, target);
            var state = tool is { } held ? ToolState(brain, held) : default;
            Note(brain, new CrewletWatch(key, owner, target, prototype, expendable, risky, facts, tick, now, state));
        }
    }

    private CrewletToolState ToolState(CrewletBrainComponent brain, EntityUid tool)
    {
        var active = TryComp<ItemToggleComponent>(tool, out var toggle) && toggle.Activated;
        if (!TryComp<SolutionContainerManagerComponent>(tool, out var manager))
            return new CrewletToolState(active, null);

        List<CrewletToolReagent>? reagents = null;
        foreach (var (name, solution) in _solutions.EnumerateSolutions((tool, manager)))
        {
            if (name == null)
                continue;

            foreach (var reagent in brain.FormReagents)
            {
                var quantity = solution.Comp.Solution.GetTotalPrototypeQuantity(reagent);
                if (quantity <= FixedPoint2.Zero)
                    continue;

                reagents ??= new List<CrewletToolReagent>();
                reagents.Add(new CrewletToolReagent(name, reagent, quantity));
            }
        }

        return new CrewletToolState(active, reagents);
    }

    private static CrewletToolState Fuller(CrewletToolState kept, CrewletToolState seen)
    {
        var reagents = Total(seen.Reagents) > Total(kept.Reagents) ? seen.Reagents : kept.Reagents;
        return new CrewletToolState(kept.Active || seen.Active, reagents);
    }

    private static FixedPoint2 Total(List<CrewletToolReagent>? reagents)
    {
        var total = FixedPoint2.Zero;
        if (reagents == null)
            return total;

        foreach (var reagent in reagents)
        {
            total += reagent.Quantity;
        }

        return total;
    }

    private void Teach(CrewletBrainComponent brain, EntityUid tool, EntityUid target)
    {
        for (var i = 0; i < brain.ToolUses.Count; i++)
        {
            var use = brain.ToolUses[i];
            if (use.Taught
                && _whitelist.IsWhitelistPass(use.Tool, tool)
                && _whitelist.IsWhitelistPass(use.Target, target))
                brain.TaughtUses.Add(i);
        }
    }

    private static void Note(CrewletBrainComponent brain, CrewletWatch watch)
    {
        var watches = brain.Watches;
        if (watches.Count > 0 && Pending(watches[^1], watch.Tick, watch.Target))
        {
            watches[^1] = watch;
            return;
        }

        foreach (var other in watches)
        {
            if (other.Key == watch.Key && other.Target == watch.Target)
                return;
        }

        if (watches.Count >= brain.MaxWatches)
            watches.RemoveAt(0);

        watches.Add(watch);
    }

    private static void Retract(CrewletBrainComponent brain, GameTick tick, EntityUid target)
    {
        var watches = brain.Watches;
        if (watches.Count > 0 && Pending(watches[^1], tick, target))
            watches.RemoveAt(watches.Count - 1);
    }

    private static bool Pending(CrewletWatch watch, GameTick tick, EntityUid target)
    {
        return watch.State == CrewletWatchState.Seen && !watch.Handled && watch.Tick == tick && watch.Target == target;
    }

    private bool TryHabit(Entity<CrewletBrainComponent> ent,
        CrewletToolAction action,
        EntityUid? tool,
        EntityUid target,
        string? verbType,
        string? verb,
        out CrewletHabitKey key)
    {
        key = default;
        EntProtoId? toolPrototype = null;
        if (tool is { } used)
        {
            if (_virtualItemQuery.HasComp(used)
                || PrototypeOf(used) is not { } usedPrototype
                || _whitelist.IsWhitelistPass(ent.Comp.HabitToolBlacklist, used)
                || (action == CrewletToolAction.Use
                    && _mobStateQuery.HasComp(target)
                    && !_whitelist.IsWhitelistPass(ent.Comp.PersonTools, used)))
                return false;

            toolPrototype = usedPrototype;
        }

        return TryHabitKey(ent, action, toolPrototype, target, verbType, verb, out key);
    }

    private bool TryHabitKey(Entity<CrewletBrainComponent> ent,
        CrewletToolAction action,
        EntProtoId? tool,
        EntityUid target,
        string? verbType,
        string? verb,
        out CrewletHabitKey key)
    {
        key = default;
        var brain = ent.Comp;
        if (target == ent.Owner
            || _crewletQuery.HasComp(target)
            || _container.IsEntityInContainer(target)
            || PrototypeOf(target) is not { } prototype
            || _whitelist.IsWhitelistPass(brain.HabitToolBlacklist, target))
            return false;

        if (action == CrewletToolAction.Use)
        {
            if (tool == null)
                return false;

            if (_mobStateQuery.HasComp(target))
            {
                if (!_humanoidQuery.HasComp(target))
                    return false;

                key = new CrewletHabitKey(action, tool, null, null, null, null);
            }
            else
            {
                if (_whitelist.IsWhitelistPass(brain.TargetBlacklist, target))
                    return false;

                var group = HabitGroup(brain, target);
                var shape = group == null ? (EntProtoId?) prototype : null;
                key = new CrewletHabitKey(action, tool, shape, null, null, group);
            }
        }
        else if (action == CrewletToolAction.Verb)
        {
            if (verbType == null || !brain.VerbTypes.Contains(verbType))
                return false;

            var mob = _mobStateQuery.HasComp(target);
            if (!mob && _whitelist.IsWhitelistPass(brain.TargetBlacklist, target))
                return false;

            var person = mob && _humanoidQuery.HasComp(target);
            key = new CrewletHabitKey(action, tool, person ? null : (EntProtoId?) prototype, verbType, verb, null);
        }
        else
        {
            if (_mobStateQuery.HasComp(target) || (action == CrewletToolAction.Hand && _itemQuery.HasComp(target)))
                return false;

            if (_vendingQuery.HasComp(target))
            {
                key = new CrewletHabitKey(CrewletToolAction.Vend, null, null, null, null, null);
            }
            else if (_whitelist.IsWhitelistPass(brain.InterfaceTargets, target)
                || _whitelist.IsWhitelistPass(brain.TargetBlacklist, target)
                || _whitelist.IsWhitelistPass(brain.ActivateBlacklist, target))
            {
                return false;
            }
            else
            {
                var group = HabitGroup(brain, target);
                var shape = group == null ? (EntProtoId?) prototype : null;
                key = new CrewletHabitKey(action, null, shape, null, null, group);
            }
        }

        return !brain.Unlearnable.Contains(key);
    }

    private string? HabitGroup(CrewletBrainComponent brain, EntityUid target)
    {
        foreach (var (group, whitelist) in brain.HabitGroups)
        {
            if (_whitelist.IsWhitelistPass(whitelist, target))
                return group;
        }

        return null;
    }

    private bool HabitFits(Entity<CrewletBrainComponent> ent, CrewletHabit habit, EntityUid uid)
    {
        var key = habit.Key;
        var shaped = key.Action switch
        {
            CrewletToolAction.Vend => _vendingQuery.HasComp(uid),
            _ when key.Group != null => HabitGroup(ent.Comp, uid) == key.Group,
            CrewletToolAction.Use or CrewletToolAction.Verb when key.Target == null => _humanoidQuery.HasComp(uid),
            _ => PrototypeOf(uid) == key.Target,
        };

        var action = key.Action == CrewletToolAction.Vend ? CrewletToolAction.Activate : key.Action;
        return shaped
            && ((Facts(ent, uid) ^ habit.Facts) & ~habit.Varied) == CrewletFacts.None
            && TryHabitKey(ent, action, key.Tool, uid, key.VerbType, key.Verb, out var found)
            && found == key
            && HabitUsable(ent, key, uid);
    }

    private bool HabitUsable(Entity<CrewletBrainComponent> ent, CrewletHabitKey key, EntityUid uid)
    {
        return key.Action switch
        {
            CrewletToolAction.Vend => CanVend(uid) && Authorized(ent, uid),
            CrewletToolAction.Activate or CrewletToolAction.Hand => Authorized(ent, uid),
            CrewletToolAction.Verb => key.Tool != null || FindVerb(ent, uid, key, true) != null,
            _ => true,
        };
    }

    private static bool Chore(CrewletHabitKey key)
    {
        return key.Action is CrewletToolAction.Activate or CrewletToolAction.Hand;
    }

    private CrewletFacts Facts(Entity<CrewletBrainComponent> ent, EntityUid uid)
    {
        if (!_mobStateQuery.TryComp(uid, out var mobState))
            return CrewletFacts.None;

        var facts = CrewletFacts.None;
        if (_mobState.IsCritical(uid, mobState))
            facts |= CrewletFacts.Critical;

        if (_mobState.IsDead(uid, mobState))
            facts |= CrewletFacts.Dead;

        if (!_actionBlocker.CanInteract(uid, null))
            facts |= CrewletFacts.Helpless;

        if (TryComp<CuffableComponent>(uid, out var cuffable) && _cuffable.IsCuffed((uid, cuffable)))
            facts |= CrewletFacts.Cuffed;

        if (_buckle.IsBuckled(uid))
            facts |= CrewletFacts.Buckled;

        if (uid == ent.Comp.KnownOwner)
            facts |= CrewletFacts.Owner;

        if (!TryComp<CrewletReputationComponent>(ent, out var reputation))
            return facts;

        var opinion = _reputation.GetOpinion((ent.Owner, reputation), uid);
        if (opinion >= reputation.FriendlyThreshold)
            facts |= CrewletFacts.Liked;
        else if (opinion <= reputation.WaryThreshold)
            facts |= CrewletFacts.Disliked;

        return facts;
    }

    private bool RunVerb(Entity<CrewletBrainComponent> ent, EntityUid target, CrewletHabitKey key)
    {
        if (FindVerb(ent, target, key, false) is not { } verb)
            return false;

        _verbs.ExecuteVerb(verb, ent, target);
        return true;
    }

    private Verb? FindVerb(Entity<CrewletBrainComponent> ent, EntityUid target, CrewletHabitKey key, bool force)
    {
        if (Verb.VerbTypes.FirstOrDefault(type => type.Name == key.VerbType) is not { } verbType)
            return null;

        foreach (var verb in _verbs.GetLocalVerbs(target, ent, verbType, force))
        {
            if (!verb.Disabled && VerbName(verb) == key.Verb)
                return verb;
        }

        return null;
    }

    private static string VerbName(Verb verb)
    {
        return $"{verb.Category?.Text} {verb.Text}".Trim();
    }

    private bool CanVend(EntityUid uid)
    {
        if (!_vendingQuery.TryComp(uid, out var machine)
            || machine.Broken
            || machine.Ejecting
            || !_power.IsPowered(uid))
            return false;

        return Stocked(machine.Inventory) || (machine.Contraband && Stocked(machine.ContrabandInventory));
    }

    private bool Authorized(EntityUid crewlet, EntityUid machine)
    {
        if (!TryComp<AccessReaderComponent>(machine, out var reader) || HasComp<EmaggedComponent>(machine))
            return true;

        var items = _accessReader.FindPotentialAccessItems(crewlet);
        var access = _accessReader.FindAccessTags(crewlet, items);
        _accessReader.FindStationRecordKeys(crewlet, out var keys, items);
        return _accessReader.IsAllowed(access, keys, machine, reader);
    }

    private static bool Stocked(Dictionary<string, VendingMachineInventoryEntry> inventory)
    {
        foreach (var entry in inventory.Values)
        {
            if (entry.Amount > 0)
                return true;
        }

        return false;
    }

    private bool Vend(Entity<CrewletBrainComponent> ent, EntityUid machine)
    {
        if (!CanVend(machine)
            || !_interaction.InRangeAndAccessible(ent.Owner, machine)
            || !_actionBlocker.CanInteract(ent, machine)
            || !_vending.IsAuthorized(machine, ent))
            return false;

        _vending.EjectRandom(machine, false);
        return true;
    }

    private void UpdateWatches(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var watches = ent.Comp.Watches;
        for (var i = watches.Count - 1; i >= 0; i--)
        {
            var watch = watches[i];
            if (Advance(ent.Comp, ref watch, now))
                watches[i] = watch;
            else
                watches.RemoveAt(i);
        }
    }

    private bool Advance(CrewletBrainComponent brain, ref CrewletWatch watch, TimeSpan now)
    {
        switch (watch.State)
        {
            case CrewletWatchState.Seen:
                if (StartedDoAfter(watch) is { } id)
                {
                    watch.State = CrewletWatchState.DoAfter;
                    watch.DoAfter = id;
                    return true;
                }

                if (now < watch.Seen + DoAfterGrace)
                    return true;

                watch.State = CrewletWatchState.Settle;
                watch.CheckAt = watch.Seen + brain.HabitSettleTime;
                return watch.Handled || watch.Expendable;
            case CrewletWatchState.DoAfter:
                if (OwnerDoAfter(watch.Owner, watch.DoAfter) is not { } doAfter
                    || doAfter.Cancelled
                    || now - watch.Seen > brain.HabitRecheckTime)
                    return false;

                if (doAfter.Completed)
                {
                    watch.State = CrewletWatchState.Settle;
                    watch.CheckAt = now + brain.HabitSettleTime;
                }

                return true;
            case CrewletWatchState.Settle:
                if (now < watch.CheckAt)
                    return true;

                if (Survives(watch) || watch.Expendable)
                {
                    Tally(brain, watch);
                    watch.State = CrewletWatchState.Recheck;
                    watch.CheckAt = now + brain.HabitRecheckTime;
                    return !watch.Expendable;
                }

                Forbid(brain, watch.Key);
                return false;
            default:
                if (Survives(watch))
                    return now < watch.CheckAt;

                Forbid(brain, watch.Key);
                return false;
        }
    }

    private DoAfter? OwnerDoAfter(EntityUid owner, ushort index)
    {
        if (!TryComp<DoAfterComponent>(owner, out var doAfters))
            return null;

        foreach (var (id, doAfter) in doAfters.DoAfters)
        {
            if (id == index)
                return doAfter;
        }

        return null;
    }

    private ushort? StartedDoAfter(CrewletWatch watch)
    {
        if (!TryComp<DoAfterComponent>(watch.Owner, out var doAfters))
            return null;

        foreach (var (id, doAfter) in doAfters.DoAfters)
        {
            if (!doAfter.Cancelled
                && !doAfter.Completed
                && doAfter.StartTime >= watch.Seen
                && (doAfter.Args.Target == watch.Target || doAfter.Args.EventTarget == watch.Target))
                return id;
        }

        return null;
    }

    private bool Survives(CrewletWatch watch)
    {
        return !TerminatingOrDeleted(watch.Target)
            && !EntityManager.IsQueuedForDeletion(watch.Target)
            && PrototypeOf(watch.Target) == watch.Prototype;
    }

    private void Tally(CrewletBrainComponent brain, CrewletWatch watch)
    {
        var key = watch.Key;
        if (brain.Unlearnable.Contains(key))
            return;

        var habits = brain.Habits;
        var index = IndexOf(habits, key);
        if (index >= 0)
        {
            var habit = habits[index];
            habit.Count++;
            habit.Varied |= habit.Facts ^ watch.Facts;
            habit.ToolState = Fuller(habit.ToolState, watch.ToolState);
            habits[index] = habit;
        }
        else
        {
            habits.Add(new CrewletHabit(key, 1, false, watch.Risky, watch.Facts, CrewletFacts.None, watch.ToolState));
            Trim(brain);
        }

        Rebalance(brain);
    }

    private void Untrain(Entity<CrewletBrainComponent> ent)
    {
        var brain = ent.Comp;
        if (brain.LastHabit is not { } key || _timing.CurTime >= brain.UntrainUntil)
            return;

        brain.LastHabit = null;
        var index = IndexOf(brain.Habits, key);
        if (index < 0)
            return;

        brain.Habits.RemoveAt(index);
        Rebalance(brain);
        _chat.TryEmoteWithChat(ent, brain.UntrainEmote, forceEmote: true);
    }

    private void Forbid(CrewletBrainComponent brain, CrewletHabitKey key)
    {
        var index = IndexOf(brain.Habits, key);
        if (index >= 0)
            brain.Habits.RemoveAt(index);

        if (!brain.Unlearnable.Contains(key))
        {
            if (brain.Unlearnable.Count >= brain.MaxUnlearnable)
                brain.Unlearnable.RemoveAt(0);

            brain.Unlearnable.Add(key);
        }

        Rebalance(brain);
    }

    private static int IndexOf(List<CrewletHabit> habits, CrewletHabitKey key)
    {
        for (var i = 0; i < habits.Count; i++)
        {
            if (habits[i].Key == key)
                return i;
        }

        return -1;
    }

    private static void Trim(CrewletBrainComponent brain)
    {
        var habits = brain.Habits;
        var candidates = 0;
        var weakest = -1;
        for (var i = 0; i < habits.Count - 1; i++)
        {
            if (habits[i].Learned)
                continue;

            candidates++;
            if (weakest < 0 || habits[i].Count < habits[weakest].Count)
                weakest = i;
        }

        if (candidates >= brain.MaxCandidates && weakest >= 0)
            habits.RemoveAt(weakest);
    }

    private static void Rebalance(CrewletBrainComponent brain)
    {
        var habits = brain.Habits;
        while (true)
        {
            var best = -1;
            var weakest = -1;
            var learned = 0;
            for (var i = 0; i < habits.Count; i++)
            {
                var habit = habits[i];
                if (habit.Learned)
                {
                    learned++;
                    if (weakest < 0 || habit.Count < habits[weakest].Count)
                        weakest = i;
                }
                else if (habit.Count >= LearnThreshold(brain, habit)
                    && (best < 0 || habit.Count > habits[best].Count))
                {
                    best = i;
                }
            }

            if (best < 0)
                return;

            if (learned >= brain.MaxHabits)
            {
                if (weakest < 0 || habits[best].Count <= habits[weakest].Count)
                    return;

                habits[weakest] = habits[weakest] with { Learned = false };
            }

            habits[best] = habits[best] with { Learned = true };
        }
    }

    private static int LearnThreshold(CrewletBrainComponent brain, CrewletHabit habit)
    {
        if (habit.Risky)
            return brain.RiskyLearnAfter;

        return Chore(habit.Key) ? brain.ChoreLearnAfter : brain.LearnAfter;
    }

    private EntProtoId? PrototypeOf(EntityUid uid)
    {
        if (!TryComp(uid, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype)
            return null;

        return new EntProtoId(prototype.ID);
    }
}

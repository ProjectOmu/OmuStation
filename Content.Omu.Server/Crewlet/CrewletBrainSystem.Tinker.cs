// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Omu.Shared.Paper;
using Content.Shared.Chat;
using Content.Shared.Crayon;
using Content.Shared.Decals;
using Content.Shared.Interaction.Components;
using Content.Shared.Paper;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private const string DefaultSignatureFont = "Default";
    private const string CrayonDecalTag = "crayon";

    private readonly List<CrewletToolChoice> _toolChoices = new();
    private readonly List<string> _drawings = new();

    private record struct CrewletToolChoice(
        CrewletToolAction Action,
        float Weight,
        EntityUid First,
        EntityUid? Target,
        EntityCoordinates Spot,
        CrewletHabitKey? Habit);

    private void RunTinker(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        switch (brain.Step)
        {
            case CrewletStep.Start:
                if (HeldItem(ent) != null)
                {
                    End(ent);
                    return;
                }

                var picked = PickToolUse(ent, now);
                if (picked != null && picked == brain.ToolTarget && brain.ToolHabit?.Tool is { } shape)
                {
                    FormTool(ent, shape, now);
                    return;
                }

                if (picked is not { } first)
                {
                    End(ent);
                    return;
                }

                brain.Target = first;
                Approach(ent, first, Handless(brain) ? brain.InteractRange : brain.MeleeRange, now);
                return;
            case CrewletStep.Approach when !Handless(brain):
                switch (Move(ent, now))
                {
                    case CrewletMove.Moving:
                        return;
                    case CrewletMove.Arrived:
                        brain.Step = CrewletStep.Act;
                        return;
                    default:
                        Improvise(ent, now);
                        return;
                }
            case CrewletStep.Approach:
            case CrewletStep.Act:
                if (!Reach(ent, now, out var reached))
                    return;

                if (Handless(brain))
                {
                    Perform(ent, null, now);
                    return;
                }

                Interact(ent, reached);
                if (!_hands.IsHolding(ent.Owner, reached))
                {
                    Improvise(ent, now);
                    return;
                }

                var range = brain.ToolAction == CrewletToolAction.Floor ? brain.WanderRange : brain.MeleeRange;
                SetCourse(ent, brain.ToolSpot, range, brain.MoveTimeout, now, CrewletStep.Escort);
                return;
            case CrewletStep.Escort:
                if (brain.Target is not { } held
                    || !_hands.IsHolding(ent.Owner, held)
                    || (brain.ToolTarget != null && !Present(brain.ToolTarget)))
                {
                    Improvise(ent, now);
                    return;
                }

                var move = Move(ent, now);
                if (move == CrewletMove.Moving)
                    return;

                if (move != CrewletMove.Arrived)
                {
                    Retry(ent);
                    return;
                }

                Perform(ent, held, now);
                return;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                    End(ent);
                return;
        }
    }

    private bool CanForm(CrewletBrainComponent brain, EntProtoId shape)
    {
        if (!_proto.TryIndex(shape, out var prototype))
            return false;

        foreach (var name in brain.FormBlacklist.Keys)
        {
            if (prototype.Components.ContainsKey(name))
                return false;
        }

        return true;
    }

    private void FormTool(Entity<CrewletBrainComponent> ent, EntProtoId shape, TimeSpan now)
    {
        var brain = ent.Comp;
        var tool = Spawn(shape, Transform(ent).Coordinates);
        AddComp<UnremoveableComponent>(tool);
        if (!_hands.TryPickup(ent, tool, checkActionBlocker: false))
        {
            Del(tool);
            End(ent);
            return;
        }

        var index = brain.ToolHabit is { } key ? IndexOf(brain.Habits, key) : -1;
        if (index >= 0)
            ApplyToolState(ent, tool, brain.Habits[index].ToolState);

        brain.FormedTool = tool;
        brain.Target = tool;
        _chat.TrySendInGameICMessage(ent,
            Loc.GetString(brain.FormToolText, ("tool", tool)),
            InGameICChatType.Emote,
            false);
        _audio.PlayPvs(brain.FormToolSound, ent);
        SetCourse(ent, brain.ToolSpot, brain.MeleeRange, brain.MoveTimeout, now, CrewletStep.Escort);
    }

    private void ApplyToolState(EntityUid user, EntityUid tool, CrewletToolState state)
    {
        if (state.Active)
            _itemToggle.TryActivate(tool, user, predicted: false);

        if (state.Reagents == null)
            return;

        foreach (var reagent in state.Reagents)
        {
            if (_solutions.TryGetSolution(tool, reagent.Solution, out var solution, out _))
                _solutions.TryAddReagent(solution.Value, reagent.Reagent, reagent.Quantity);
        }
    }

    private bool ToolReady(CrewletBrainComponent brain, EntityUid tool, CrewletToolState state)
    {
        if (state.Active && !_itemToggle.IsActivated(tool))
            return false;

        if (state.Reagents == null)
            return true;

        foreach (var reagent in state.Reagents)
        {
            if (!_solutions.TryGetSolution(tool, reagent.Solution, out _, out var solution)
                || solution.GetTotalPrototypeQuantity(reagent.Reagent) < reagent.Quantity * brain.ToolStockFraction)
                return false;
        }

        return true;
    }

    private void Perform(Entity<CrewletBrainComponent> ent, EntityUid? tool, TimeSpan now)
    {
        var brain = ent.Comp;
        if (now < brain.NextVerbTry)
            return;

        var nextId = NextDoAfter(ent);
        if (!ApplyTool(ent, tool))
        {
            if (brain.ToolAction != CrewletToolAction.Verb || now >= brain.StepEnds)
            {
                Improvise(ent, now);
                return;
            }

            brain.NextVerbTry = now + brain.VerbRetryInterval;
            var retry = now + brain.VerbRetryTime;
            if (retry < brain.StepEnds)
                brain.StepEnds = retry;

            return;
        }

        var wait = DoAfterWait(ent, nextId);
        brain.LastHabit = brain.ToolHabit;
        brain.UntrainUntil = now + wait + brain.UntrainWindow;

        WaitFor(ent, wait, now);
    }

    private void Improvise(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Target is { } tried && tried != brain.FormedTool)
            Cooldown(ent, tried, brain.TargetCooldown, now);

        if (brain.FormedTool != null
            || brain.ToolHabit?.Tool is not { } shape
            || !Present(brain.ToolTarget)
            || !CanForm(brain, shape))
        {
            Retry(ent);
            return;
        }

        if (brain.Target is { } held && _hands.IsHolding(ent.Owner, held))
            _hands.TryDrop(ent.Owner, held);

        FormTool(ent, shape, now);
    }

    private void Retry(Entity<CrewletBrainComponent> ent)
    {
        var brain = ent.Comp;
        var activity = brain.Activity;
        var tries = brain.Count + 1;
        End(ent);
        if (tries > brain.HabitRetries)
            return;

        brain.Activity = activity;
        brain.Count = tries;
    }

    private static bool Handless(CrewletBrainComponent brain)
    {
        return brain.ToolAction is CrewletToolAction.Hand or CrewletToolAction.Activate or CrewletToolAction.Vend
            || (brain.ToolAction == CrewletToolAction.Verb && brain.ToolHabit?.Tool == null);
    }

    private EntityUid? PickToolUse(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        _toolChoices.Clear();
        _scan.Clear();
        _lookup.GetEntitiesInRange(ent.Owner,
            brain.VisionRadius,
            _scan,
            LookupFlags.Uncontained | LookupFlags.Approximate);

        var total = 0f;
        if (brain.Activity != CrewletActivity.Habit)
        {
            for (var i = 0; i < brain.ToolUses.Count; i++)
            {
                if (!brain.ToolUses[i].Taught || brain.TaughtUses.Contains(i))
                    total += AddToolChoice(ent, brain.ToolUses[i], now);
            }
        }

        foreach (var habit in brain.Habits)
        {
            if (habit.Learned)
                total += AddHabitChoice(ent, habit, now);
        }

        if (_toolChoices.Count > 1 && brain.LastHabit is { } last)
        {
            for (var i = 0; i < _toolChoices.Count; i++)
            {
                if (_toolChoices[i].Habit != last)
                    continue;

                total -= _toolChoices[i].Weight;
                _toolChoices.RemoveAt(i);
                break;
            }
        }

        _scan.Clear();
        if (total <= 0f)
            return null;

        var roll = _random.NextFloat() * total;
        var chosen = _toolChoices[^1];
        foreach (var choice in _toolChoices)
        {
            roll -= choice.Weight;
            if (roll >= 0f)
                continue;

            chosen = choice;
            break;
        }

        _toolChoices.Clear();
        brain.ToolAction = chosen.Action;
        brain.ToolTarget = chosen.Target;
        brain.ToolSpot = chosen.Spot;
        brain.ToolHabit = chosen.Habit;
        if (chosen.Target is { } used)
            Cooldown(ent, used, brain.TargetCooldown, now);

        return chosen.First;
    }

    private float AddToolChoice(Entity<CrewletBrainComponent> ent, CrewletToolUse use, TimeSpan now)
    {
        if (use.Weight <= 0f
            || (use.Action == CrewletToolAction.Sign && ent.Comp.KnownOwner == null)
            || FindTool(ent, use, default, now) is not { } tool)
            return 0f;

        if (use.Action != CrewletToolAction.Floor)
        {
            return FindToolTarget(ent, use, default, tool, now) is { } target
                ? AddChoice(use.Action, use.Weight, tool, target, null)
                : 0f;
        }

        if (PickSpot(ent, ent.Owner, ent.Comp.ToolFloorRange) is not { } floor)
            return 0f;

        _toolChoices.Add(new CrewletToolChoice(use.Action, use.Weight, tool, null, floor, null));
        return use.Weight;
    }

    private float AddHabitChoice(Entity<CrewletBrainComponent> ent, CrewletHabit habit, TimeSpan now)
    {
        var key = habit.Key;
        EntityUid? tool = null;
        if (key.Tool is { } shape)
        {
            tool = FindTool(ent, null, habit, now);
            if (tool == null && !CanForm(ent.Comp, shape))
                return 0f;
        }

        if (FindToolTarget(ent, null, habit, tool, now) is not { } target)
            return 0f;

        var weight = MathF.Min(habit.Count * ent.Comp.HabitWeight, ent.Comp.MaxHabitWeight);
        if (Chore(key))
            weight *= ent.Comp.ChoreWeight;

        return AddChoice(key.Action, weight, tool ?? target, target, key);
    }

    private float AddChoice(CrewletToolAction action,
        float weight,
        EntityUid first,
        EntityUid target,
        CrewletHabitKey? habit)
    {
        _toolChoices.Add(new CrewletToolChoice(action,
            weight,
            first,
            target,
            new EntityCoordinates(target, Vector2.Zero),
            habit));
        return weight;
    }

    private EntityUid? FindTool(Entity<CrewletBrainComponent> ent,
        CrewletToolUse? use,
        CrewletHabit habit,
        TimeSpan now)
    {
        var origin = _transform.GetWorldPosition(ent);
        EntityUid? best = null;
        var bestDistance = ent.Comp.VisionRadius;
        foreach (var uid in ent.Comp.Items)
        {
            if (!IsCandidate(ent, uid, CrewletPick.Loot, now) || !ToolFits(ent, use, habit, uid))
                continue;

            var distance = (_transform.GetWorldPosition(uid) - origin).Length();
            if (distance > bestDistance)
                continue;

            best = uid;
            bestDistance = distance;
        }

        return best is { } tool && InSight(ent, tool) ? tool : null;
    }

    private EntityUid? FindToolTarget(Entity<CrewletBrainComponent> ent,
        CrewletToolUse? use,
        CrewletHabit habit,
        EntityUid? tool,
        TimeSpan now)
    {
        var origin = _transform.GetWorldPosition(ent);
        EntityUid? best = null;
        var bestDistance = ent.Comp.VisionRadius;
        foreach (var uid in _scan)
        {
            if (uid == ent.Owner
                || uid == tool
                || TerminatingOrDeleted(uid)
                || !TargetReady(ent.Comp, uid, now)
                || _container.IsEntityInContainer(uid)
                || (use != null ? !UseFits(ent, use, uid) : !HabitFits(ent, habit, uid)))
                continue;

            var distance = (_transform.GetWorldPosition(uid) - origin).Length();
            if (distance > bestDistance)
                continue;

            best = uid;
            bestDistance = distance;
        }

        return best is { } target && InSight(ent, target) ? target : null;
    }

    private bool ToolFits(Entity<CrewletBrainComponent> ent, CrewletToolUse? use, CrewletHabit habit, EntityUid uid)
    {
        if (use != null)
            return _whitelist.IsWhitelistPass(use.Tool, uid);

        return PrototypeOf(uid) == habit.Key.Tool
            && !_whitelist.IsWhitelistPass(ent.Comp.HabitToolBlacklist, uid)
            && ToolReady(ent.Comp, uid, habit.ToolState);
    }

    private bool UseFits(Entity<CrewletBrainComponent> ent, CrewletToolUse use, EntityUid uid)
    {
        return !_mobStateQuery.HasComp(uid)
            && _whitelist.IsWhitelistPass(use.Target, uid)
            && !_whitelist.IsWhitelistPass(ent.Comp.TargetBlacklist, uid)
            && !_whitelist.IsWhitelistPass(use.TargetBlacklist, uid)
            && (use.Action != CrewletToolAction.Sign || Unsigned(ent, uid));
    }

    private bool ApplyTool(Entity<CrewletBrainComponent> ent, EntityUid? tool)
    {
        var brain = ent.Comp;
        switch (brain.ToolAction)
        {
            case CrewletToolAction.Floor:
                if (tool is { } crayon)
                    PickDrawing(crayon);

                _interaction.UserInteraction(ent, brain.ToolSpot, null);
                return true;
            case CrewletToolAction.Sign:
                if (tool is not { } pen
                    || brain.ToolTarget is not { } paper
                    || brain.KnownOwner is not { } owner
                    || !TryComp<PaperComponent>(paper, out var sheet)
                    || !_interaction.InRangeAndAccessible(ent.Owner, paper)
                    || !_actionBlocker.CanInteract(ent, paper))
                    return false;

                var font = _signatureFont.GetFont(owner, pen) ?? DefaultSignatureFont;
                EnsureComp<SignatureFontComponent>(ent).Font = font;
                return _signature.TrySignPaper((paper, sheet), ent, pen);
            case CrewletToolAction.Activate:
                if (brain.ToolTarget is not { } device)
                    return false;

                _interaction.InteractionActivate(ent, device);
                return true;
            case CrewletToolAction.Vend:
                return brain.ToolTarget is { } machine && Vend(ent, machine);
            case CrewletToolAction.Verb:
                return brain.ToolTarget is { } subject
                    && brain.ToolHabit is { } learned
                    && RunVerb(ent, subject, learned);
            default:
                if (brain.ToolTarget is not { } target)
                    return false;

                Interact(ent, target);
                return true;
        }
    }

    private void PickDrawing(EntityUid crayon)
    {
        _drawings.Clear();
        foreach (var decal in _proto.EnumeratePrototypes<DecalPrototype>())
        {
            if (decal.Tags.Contains(CrayonDecalTag))
                _drawings.Add(decal.ID);
        }

        if (_drawings.Count > 0)
            RaiseLocalEvent(crayon, new CrayonSelectMessage(_random.Pick(_drawings)));
    }

    private bool Unsigned(EntityUid crewlet, EntityUid paper)
    {
        if (!TryComp<PaperComponent>(paper, out var sheet))
            return false;

        var name = _idCard.TryFindIdCard(crewlet, out var id) && !string.IsNullOrWhiteSpace(id.Comp.FullName)
            ? id.Comp.FullName
            : Name(crewlet);

        foreach (var stamp in sheet.StampedBy)
        {
            if (stamp.StampedName == name)
                return false;
        }

        return true;
    }
}

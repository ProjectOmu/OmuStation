// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Robust.Shared.Map;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private void OnOwnerDamaged(Entity<CrewletOwnerComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        var dealt = 0f;
        foreach (var amount in delta.DamageDict.Values)
        {
            if (amount > FixedPoint2.Zero)
                dealt += amount.Float();
        }

        var now = _timing.CurTime;
        foreach (var uid in ent.Comp.Crewlets)
        {
            if (!TryComp<CrewletBrainComponent>(uid, out var brain))
                continue;

            if (args.Origin is { } origin && InSight((uid, brain), ent))
                _reputation.OwnerHurt(uid, ent, origin, dealt);

            if (brain.Active && dealt > brain.WorryDamage)
                brain.WorryUntil = now + brain.WorryMemory;
        }
    }

    private bool WantsWorry(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (now >= brain.WorryUntil
            || brain.KnownOwner is not { } owner
            || _mobState.IsDead(owner)
            || !IsReady(brain, CrewletActivity.Worry, now))
            return false;

        brain.WorryUntil = TimeSpan.Zero;
        brain.Candidate = owner;
        return true;
    }

    private void RunWorry(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.KnownOwner is not { } owner || !Present(owner))
        {
            End(ent);
            return;
        }

        switch (brain.Step)
        {
            case CrewletStep.Start:
                _chat.TryEmoteWithChat(ent, brain.WorryEmote, forceEmote: true);
                if (HeldItem(ent) != null)
                    _hands.TryDrop(ent.Owner);

                if (HeldItem(ent) == null && WorryTool(ent, now) is { } tool)
                    Begin(ent, tool, brain.MeleeRange, now);
                else
                    Approach(ent, owner, brain.MeleeRange, now);
                return;
            case CrewletStep.Approach:
            case CrewletStep.Act:
                if (!Reach(ent, now, out var reached))
                    return;

                if (reached == owner)
                {
                    if (HeldItem(ent) == null)
                        Interact(ent, owner);

                    WaitFor(ent, brain.HugRest, now);
                    return;
                }

                Interact(ent, reached);
                if (!_hands.IsHolding(ent.Owner, reached))
                {
                    End(ent);
                    return;
                }

                SetCourse(ent,
                    new EntityCoordinates(owner, Vector2.Zero),
                    brain.MeleeRange,
                    brain.MoveTimeout,
                    now,
                    CrewletStep.Escort);
                return;
            case CrewletStep.Escort:
                var move = Move(ent, now);
                if (move == CrewletMove.Moving)
                    return;

                if (move != CrewletMove.Arrived)
                {
                    End(ent);
                    return;
                }

                var nextId = NextDoAfter(ent);
                Interact(ent, owner);
                WaitFor(ent, DoAfterWait(ent, nextId), now);
                return;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                    End(ent);
                return;
        }
    }

    private EntityUid? WorryTool(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        foreach (var habit in ent.Comp.Habits)
        {
            if (habit.Learned
                && habit.Key is { Action: CrewletToolAction.Use, Target: null, Group: null }
                && FindTool(ent, null, habit, now) is { } tool)
                return tool;
        }

        return null;
    }
}

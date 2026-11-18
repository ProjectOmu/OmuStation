// SPDX-License-Identifier: MIT

using Robust.Shared.Random;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private bool Begin(Entity<CrewletBrainComponent> ent, EntityUid? picked, float range, TimeSpan now)
    {
        if (picked is not { } target)
        {
            End(ent);
            return false;
        }

        ent.Comp.Target = target;
        Cooldown(ent, target, ent.Comp.TargetCooldown, now);
        Approach(ent, target, range, now);
        return true;
    }

    private bool Reach(Entity<CrewletBrainComponent> ent, TimeSpan now, out EntityUid target)
    {
        target = default;
        if (ent.Comp.Target is not { } current || !Present(current))
        {
            End(ent);
            return false;
        }

        target = current;
        if (ent.Comp.Step != CrewletStep.Approach)
            return true;

        switch (Move(ent, now))
        {
            case CrewletMove.Moving:
                return false;
            case CrewletMove.Arrived:
                ent.Comp.Step = CrewletStep.Act;
                return true;
            default:
                End(ent);
                return false;
        }
    }

    private void RunHug(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Wait)
        {
            if (now >= ent.Comp.StepEnds)
                End(ent);
            return;
        }

        if (ent.Comp.Step == CrewletStep.Start
            && !Begin(ent, PickPerson(ent, true, now), ent.Comp.MeleeRange, now))
            return;

        if (!Reach(ent, now, out var person))
            return;

        Interact(ent, person);
        WaitFor(ent, ent.Comp.HugRest, now);
    }

    private void RunLoot(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Start)
        {
            if (HeldItem(ent) != null)
            {
                End(ent);
                return;
            }

            if (PickNearest(ent, ent.Comp.Items, CrewletPick.Loot, now) is not { } loot)
            {
                End(ent);
                return;
            }

            Point(ent, loot);
            Begin(ent, loot, ent.Comp.MeleeRange, now);
        }

        if (!Reach(ent, now, out var item))
            return;

        Interact(ent, item);
        if (_hands.IsHolding(ent.Owner, item))
            ent.Comp.GiftRecipient = PickRecipient(ent);

        End(ent);
    }

    private void RunClimb(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Wait)
        {
            if (now >= ent.Comp.StepEnds)
                End(ent);
            return;
        }

        if (ent.Comp.Step == CrewletStep.Start
            && !Begin(ent, PickNearest(ent, ent.Comp.Tables, CrewletPick.Table, now), ent.Comp.MeleeRange, now))
            return;

        if (!Reach(ent, now, out var table))
            return;

        if (!UseAlternative(ent, table, now))
            End(ent);
    }

    private void RunKnock(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Step == CrewletStep.Wait)
        {
            if (now < brain.StepEnds)
                return;

            brain.Step = CrewletStep.Act;
        }

        if (brain.Step == CrewletStep.Start
            && !Begin(ent, PickNearest(ent, brain.Windows, CrewletPick.Window, now), brain.InteractRange, now))
            return;

        if (!Reach(ent, now, out var window))
            return;

        Interact(ent, window);
        brain.Count++;
        if (brain.Count >= brain.KnockCount)
            End(ent);
        else
            WaitFor(ent, brain.KnockInterval, now);
    }

    private void RunPet(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Start
            && !Begin(ent, PickNearest(ent, ent.Comp.Animals, CrewletPick.Animal, now), ent.Comp.MeleeRange, now))
            return;

        if (!Reach(ent, now, out var animal))
            return;

        Interact(ent, animal);
        End(ent);
    }

    private void RunGlance(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Step == CrewletStep.Wait && now < brain.StepEnds)
            return;

        if (brain.Count >= brain.GlanceCount)
        {
            End(ent);
            return;
        }

        brain.Count++;
        _rotate.TryFaceAngle(ent, _random.NextAngle());
        WaitFor(ent, _random.Next(brain.MinGlanceTime, brain.MaxGlanceTime), now);
    }

    private void RunSit(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Start
            && !Begin(ent, PickNearest(ent, ent.Comp.Seats, CrewletPick.Seat, now), ent.Comp.MeleeRange, now))
            return;

        if (!Reach(ent, now, out var seat))
            return;

        Interact(ent, seat);
        End(ent);
    }

    private void RunHonk(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.KnownOwner is not { } owner)
        {
            End(ent);
            return;
        }

        switch (brain.Step)
        {
            case CrewletStep.Start:
                if (HeldItem(ent) != null)
                {
                    End(ent);
                    return;
                }

                Begin(ent, PickNearest(ent, brain.Items, CrewletPick.Horn, now), brain.MeleeRange, now);
                return;
            case CrewletStep.Approach:
            case CrewletStep.Act:
                if (!Reach(ent, now, out var horn))
                    return;

                Interact(ent, horn);
                if (!_hands.IsHolding(ent.Owner, horn))
                {
                    End(ent);
                    return;
                }

                brain.Count = brain.HonkHops;
                SetCourse(ent, new(owner, default), brain.FollowCloseRange, brain.MoveTimeout, now, CrewletStep.Escort);
                return;
        }

        if (brain.Target is not { } held || !_hands.IsHolding(ent.Owner, held))
        {
            End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Escort)
        {
            if (Move(ent, now) != CrewletMove.Moving)
                WaitFor(ent, _random.Next(brain.MinHonkWait, brain.MaxHonkWait), now);
            return;
        }

        if (now < brain.StepEnds)
            return;

        if (_random.Prob(brain.HonkChance))
            Interact(ent, held);

        brain.Count--;
        if (brain.Count <= 0)
            End(ent);
        else
            SetCourse(ent, new(owner, default), brain.FollowCloseRange, brain.MoveTimeout, now, CrewletStep.Escort);
    }

    private void RunMop(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Step == CrewletStep.Wait)
        {
            if (now >= brain.StepEnds)
                End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Start
            && !Begin(ent, PickNearest(ent, brain.Puddles, CrewletPick.Puddle, now), brain.MopRange, now))
            return;

        if (!Reach(ent, now, out _))
            return;

        _audio.PlayPvs(brain.MopSound, ent);

        _scan.Clear();
        _lookup.GetEntitiesInRange(ent.Owner, brain.MopRadius, _scan, LookupFlags.Uncontained);
        foreach (var uid in _scan)
        {
            if (_puddleQuery.HasComp(uid))
                QueueDel(uid);
        }

        _scan.Clear();
        WaitFor(ent, brain.MopWait, now);
    }
}

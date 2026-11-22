// SPDX-License-Identifier: MIT

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private bool WantsTagged(Entity<CrewletBrainComponent> ent)
    {
        if (ent.Comp.Tagger is not { } tagger)
            return false;

        if (Playing(tagger, ent))
            return true;

        ent.Comp.Tagger = null;
        return false;
    }

    private void RunTag(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Tagger is { } tagger)
        {
            brain.Tagger = null;
            brain.Target = tagger;
            brain.Count = brain.TagCount;
            StopMoving(ent);
            brain.Step = CrewletStep.Act;
            brain.StepEnds = now + brain.TagHeadStart;
        }
        else if (brain.Step == CrewletStep.Start)
        {
            if (PickNearest(ent, brain.Crewlets, CrewletPick.Playmate, now) is not { } playmate)
            {
                End(ent);
                return;
            }

            brain.Target = playmate;
            Approach(ent, playmate, brain.MeleeRange, now);
        }

        if (brain.Target is not { } partner || !StillPlaying(ent, partner))
        {
            StopPlaying(ent, now);
            return;
        }

        switch (brain.Step)
        {
            case CrewletStep.Act:
                if (now >= brain.StepEnds)
                    Approach(ent, partner, brain.MeleeRange, now);
                return;
            case CrewletStep.Approach:
                var move = Move(ent, now);
                if (move == CrewletMove.Moving)
                    return;

                if (move != CrewletMove.Arrived)
                {
                    StopPlaying(ent, now);
                    return;
                }

                brain.Count++;
                Interact(ent, partner);
                if (brain.Count >= brain.TagExchanges)
                {
                    StopPlaying(ent, now);
                    return;
                }

                if (PickSpot(ent, ent.Owner, brain.TagRunRange) is { } spot)
                    SetCourse(ent, spot, brain.WanderRange, brain.MoveTimeout, now, CrewletStep.Escort);
                else
                    WaitFor(ent, brain.TagWait, now);
                return;
            case CrewletStep.Escort:
                if (Move(ent, now) != CrewletMove.Moving)
                    WaitFor(ent, brain.TagWait, now);
                return;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                    StopPlaying(ent, now);
                return;
        }
    }

    private bool StillPlaying(Entity<CrewletBrainComponent> ent, EntityUid partner)
    {
        if (!AliveNearby(ent, partner) || !TryComp<CrewletBrainComponent>(partner, out var other))
            return false;

        return Playing(partner, ent)
            || (IdleTier(other) && (other.Tagger == ent.Owner || ent.Comp.Count == 0));
    }

    private void StopPlaying(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Target is { } partner)
            Cooldown(ent, partner, ent.Comp.TagCooldown, now);

        End(ent);
    }

    private bool Playing(EntityUid crewlet, EntityUid partner)
    {
        return TryComp<CrewletBrainComponent>(crewlet, out var brain)
            && brain.Active
            && brain.Activity == CrewletActivity.Tag
            && brain.Target == partner;
    }

    private static bool IdleTier(CrewletBrainComponent brain)
    {
        return brain.Active && brain.Activity >= CrewletActivity.Hug && brain.Activity != CrewletActivity.Tag;
    }
}

// SPDX-License-Identifier: MIT

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private bool WantsRetaliate(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.RetaliationTarget is not { } target)
            return false;

        if (now >= brain.RetaliateUntil || !Present(target) || !_mobState.IsAlive(target))
        {
            brain.RetaliationTarget = null;
            return false;
        }

        return now >= brain.RestUntil
            && DistanceTo(ent, target) <= brain.VisionRadius
            && IsReady(brain, CrewletActivity.Retaliate, now);
    }

    private void RunRetaliate(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.RetaliationTarget is not { } target
            || now >= brain.RetaliateUntil
            || !Present(target)
            || !_mobState.IsAlive(target))
        {
            End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Start)
            Approach(ent, target, brain.MeleeRange, now);

        if (brain.Step == CrewletStep.Approach)
        {
            switch (Move(ent, now))
            {
                case CrewletMove.Moving:
                    return;
                case CrewletMove.Failed:
                    Fail(ent, now);
                    return;
                case CrewletMove.TimedOut:
                    End(ent);
                    return;
            }

            brain.Step = CrewletStep.Act;
        }

        if (DistanceTo(ent, target) > brain.InteractRange)
        {
            Approach(ent, target, brain.MeleeRange, now);
            return;
        }

        if (!Strike(ent, target))
            return;

        Complain(ent, target, now);
        brain.RestUntil = now + brain.RetaliationRest;
        End(ent);
    }

    private void RunRest(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Start)
            WaitFor(ent, ent.Comp.RestUntil - now, now);

        if (now >= ent.Comp.RestUntil)
            End(ent);
    }

    private void RunPunch(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Step == CrewletStep.Start)
        {
            if (PickPerson(ent, false, now) is not { } victim)
            {
                End(ent);
                return;
            }

            brain.Target = victim;
            Approach(ent, victim, brain.MeleeRange, now);
        }

        if (brain.Target is not { } target || !Present(target) || !_mobState.IsAlive(target))
        {
            End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Approach)
        {
            switch (Move(ent, now))
            {
                case CrewletMove.Moving:
                    return;
                case CrewletMove.Arrived:
                    brain.Step = CrewletStep.Act;
                    brain.StepEnds = now + brain.StrikeTimeout;
                    break;
                default:
                    Fail(ent, now);
                    return;
            }
        }

        if (now >= brain.StepEnds || DistanceTo(ent, target) > brain.InteractRange)
        {
            End(ent);
            return;
        }

        if (!Strike(ent, target))
            return;

        Complain(ent, target, now);
        brain.RestUntil = now + brain.PunchRest;
        End(ent);
    }

    private bool FightCrewlet(Entity<CrewletBrainComponent> ent, EntityUid other, TimeSpan now)
    {
        var brain = ent.Comp;
        if (!_crewletQuery.HasComp(other))
            return true;

        if (now < brain.CrewletTruceUntil)
            return false;

        if (now - brain.LastCrewletFight > brain.CrewletFightGap)
            brain.CrewletFightStart = now;

        brain.LastCrewletFight = now;
        if (now - brain.CrewletFightStart < brain.CrewletFightTime)
            return true;

        brain.CrewletTruceUntil = now + brain.CrewletTruceTime;
        if (brain.RetaliationTarget is { } target && _crewletQuery.HasComp(target))
            brain.RetaliationTarget = null;

        return false;
    }

    private bool Strike(Entity<CrewletBrainComponent> ent, EntityUid target, bool shove = false)
    {
        if (!FightCrewlet(ent, target, _timing.CurTime))
            return false;

        if (!_melee.TryGetWeapon(ent, out var weaponUid, out var weapon))
        {
            if (!TryComp(ent, out weapon))
                return false;

            weaponUid = ent;
        }

        ent.Comp.InCombat = true;
        _combat.SetInCombatMode(ent, true);
        return shove
            ? _melee.AttemptDisarmAttack(ent, weaponUid, weapon, target)
            : _melee.AttemptLightAttack(ent, weaponUid, weapon, target);
    }
}

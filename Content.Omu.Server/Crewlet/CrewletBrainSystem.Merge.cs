// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Crewlet;
using Content.Server.NPC.HTN;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private void InitializeMerge()
    {
        SubscribeLocalEvent<CrewletBrainComponent, CrewletMergeDoAfterEvent>(OnMergeDoAfter);
    }

    private bool WantsMerge(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (!brain.Scanned
            || brain.Anomalies.Count == 0
            || !IsReady(brain, CrewletActivity.Merge, now)
            || PickNearest(ent, brain.Anomalies, CrewletPick.Anomaly, now, brain.MergeNoticeRange) is not { } anomaly)
            return false;

        brain.Candidate = anomaly;
        return true;
    }

    private void RunMerge(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Target is not { } anomaly || !_merge.CanMerge(anomaly))
        {
            End(ent);
            return;
        }

        switch (brain.Step)
        {
            case CrewletStep.Start:
                Approach(ent, anomaly, brain.MeleeRange, now);
                return;
            case CrewletStep.Approach:
                var move = Move(ent, now);
                if (move == CrewletMove.Moving)
                    return;

                if (move != CrewletMove.Arrived || !StartMerge(ent, anomaly))
                {
                    Fail(ent, now);
                    return;
                }

                WaitFor(ent, brain.MergeTime + DoAfterGrace, now);
                return;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                    End(ent);
                return;
        }
    }

    private bool StartMerge(Entity<CrewletBrainComponent> ent, EntityUid anomaly)
    {
        var args = new DoAfterArgs(EntityManager,
            ent,
            ent.Comp.MergeTime,
            new CrewletMergeDoAfterEvent(),
            ent,
            target: anomaly)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MultiplyDelay = false,
        };

        if (!_doAfter.TryStartDoAfter(args, out ent.Comp.ActivityDoAfter))
            return false;

        _rotate.TryFaceCoordinates(ent, _transform.GetWorldPosition(anomaly));
        var name = Identity.Entity(ent, EntityManager);
        _popup.PopupEntity(Loc.GetString(ent.Comp.MergeStartText, ("crewlet", name)), ent, PopupType.LargeCaution);
        _audio.PlayPvs(ent.Comp.MergeStartSound, ent);
        return true;
    }

    private void OnMergeDoAfter(Entity<CrewletBrainComponent> ent, ref CrewletMergeDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var tracked = ent.Comp.ActivityDoAfter != null;
        ent.Comp.ActivityDoAfter = null;

        var name = Identity.Entity(ent, EntityManager);
        if (args.Cancelled
            || args.Target is not { } anomaly
            || !_crewletQuery.TryComp(ent, out var crewlet)
            || !_merge.TryMerge((ent, crewlet), anomaly))
        {
            ent.Comp.ActivityReadyAt[CrewletActivity.Merge] = _timing.CurTime + ent.Comp.MergeRetry;
            if (tracked && ent.Comp.Activity == CrewletActivity.Merge)
                End(ent);

            return;
        }

        StopThinking(ent);
        RemComp<HTNComponent>(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.MergeText, ("crewlet", name)), anomaly, PopupType.LargeCaution);
        _audio.PlayPvs(ent.Comp.MergeSound, anomaly);
    }
}

// SPDX-License-Identifier: MIT

using Content.Shared.DoAfter;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private const int WanderAttempts = 3;

    private static readonly TimeSpan AlternativeWait = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DoAfterGrace = TimeSpan.FromSeconds(0.5);

    private bool WantsWakeSleeper(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (!ent.Comp.Scanned
            || ent.Comp.Sleepers.Count == 0
            || !IsReady(ent.Comp, CrewletActivity.WakeSleeper, now)
            || PickNearest(ent, ent.Comp.Sleepers, CrewletPick.Sleeper, now) is not { } sleeper)
            return false;

        ent.Comp.Candidate = sleeper;
        return true;
    }

    private bool WantsEat(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (!ent.Comp.Scanned
            || ent.Comp.Edibles.Count == 0
            || (!IsHungry(ent) && !IsThirsty(ent))
            || !IsReady(ent.Comp, CrewletActivity.Eat, now)
            || PickNearest(ent, ent.Comp.Edibles, CrewletPick.Food, now) is not { } food)
            return false;

        ent.Comp.Candidate = food;
        return true;
    }

    private void RunWakeSleeper(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Target is not { } sleeper || !Present(sleeper) || !_sleepingQuery.HasComp(sleeper))
        {
            End(ent);
            return;
        }

        if (ent.Comp.Step == CrewletStep.Start)
        {
            Cooldown(ent, sleeper, ent.Comp.TargetCooldown, now);
            Approach(ent, sleeper, ent.Comp.MeleeRange, now);
        }

        var move = Move(ent, now);
        if (move == CrewletMove.Moving)
            return;

        if (move == CrewletMove.Arrived)
            Interact(ent, sleeper);

        End(ent);
    }

    private void RunEat(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Step == CrewletStep.Wait)
        {
            if (now >= brain.StepEnds)
                End(ent);
            return;
        }

        if (brain.Target is not { } food || !Present(food))
        {
            End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Start)
            Approach(ent, food, brain.MeleeRange, now);

        var move = Move(ent, now);
        if (move == CrewletMove.Moving)
            return;

        if (move != CrewletMove.Arrived || !UseAlternative(ent, food, now))
        {
            Cooldown(ent, food, brain.TargetCooldown, now);
            Fail(ent, now);
        }
    }

    private void RunWander(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Start)
        {
            if (PickSpot(ent, ent.Comp.KnownOwner ?? ent.Owner, ent.Comp.FollowCloseRange) is not { } spot)
            {
                Switch(ent, CrewletActivity.Idle, now);
                return;
            }

            SetCourse(ent, spot, ent.Comp.WanderRange, ent.Comp.MoveTimeout, now);
        }

        if (Move(ent, now) == CrewletMove.Moving)
            return;

        Switch(ent, CrewletActivity.Idle, now);
    }

    private void RunIdle(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Step == CrewletStep.Start)
        {
            WaitFor(ent, _random.Next(brain.MinIdleTime, brain.MaxIdleTime), now);
            return;
        }

        if (now < brain.StepEnds)
            return;

        SayCreepyLine(ent);

        var roll = _random.NextFloat();
        foreach (var (activity, weight) in brain.IdleWeights)
        {
            roll -= weight;
            if (roll >= 0f)
                continue;

            if (IsReady(brain, activity, now))
            {
                Switch(ent, activity, now);
                return;
            }

            break;
        }

        End(ent);
    }

    private EntityCoordinates? PickSpot(Entity<CrewletBrainComponent> ent, EntityUid anchor, float radius)
    {
        var origin = _transform.GetMapCoordinates(anchor);
        if (origin.MapId != Transform(ent).MapID)
            origin = _transform.GetMapCoordinates(ent);

        for (var attempt = 0; attempt < WanderAttempts; attempt++)
        {
            var spot = origin.Offset(_random.NextVector2(radius));
            var coordinates = ToGridCoordinates(spot);
            if (_turf.GetTileRef(coordinates) is { } tile
                && !_turf.IsSpace(tile)
                && !_turf.IsTileBlocked(tile, CollisionGroup.MobMask)
                && !NearDisliked(ent, spot.Position))
                return coordinates;
        }

        return null;
    }

    private bool UseAlternative(Entity<CrewletBrainComponent> ent, EntityUid target, TimeSpan now)
    {
        var nextId = NextDoAfter(ent);

        if (_combat.IsInCombatMode(ent))
            _combat.SetInCombatMode(ent, false);

        if (!_interaction.AltInteract(ent, target))
            return false;

        WaitFor(ent, DoAfterWait(ent, nextId), now);
        return true;
    }

    private ushort NextDoAfter(EntityUid uid)
    {
        return CompOrNull<DoAfterComponent>(uid)?.NextId ?? 0;
    }

    private TimeSpan DoAfterWait(EntityUid uid, ushort nextId)
    {
        if (!TryComp<DoAfterComponent>(uid, out var doAfter) || doAfter.NextId == nextId)
            return AlternativeWait;

        foreach (var (id, started) in doAfter.DoAfters)
        {
            if (id == nextId)
                return started.Args.Delay + DoAfterGrace;
        }

        return AlternativeWait;
    }
}

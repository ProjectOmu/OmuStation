// SPDX-License-Identifier: MIT

using Content.Shared.Bed.Cryostorage;
using Content.Shared.Popups;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private void TrackOwner(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        EntityUid? owner = null;
        if (_crewletQuery.TryComp(ent, out var crewlet) && Present(crewlet.CrewletOwner))
            owner = crewlet.CrewletOwner;

        if (crewlet != null && owner is { } stored && HasComp<CryostorageContainedComponent>(stored))
        {
            brain.OwnerCryoSince ??= now;
            if (now - brain.OwnerCryoSince.Value >= brain.CryoOrphanTime)
            {
                brain.OwnerCryoSince = null;
                _crewlet.SetOwner((ent.Owner, crewlet), null);
                owner = null;
            }
        }
        else
        {
            brain.OwnerCryoSince = null;
        }

        if (owner != brain.KnownOwner)
        {
            if (brain.GiftRecipient == brain.KnownOwner)
                brain.GiftRecipient = null;

            brain.KnownOwner = owner;
            brain.ApartSince = null;
            brain.Greeted = false;
            if (FollowsOwner(brain.Activity))
                End(ent);
        }

        brain.OwnerDistance = owner is { } current ? DistanceTo(ent, current) : float.PositiveInfinity;
        if (float.IsPositiveInfinity(brain.OwnerDistance) || brain.OwnerDistance <= brain.CatchUpRange)
            brain.ApartSince = null;
        else
            brain.ApartSince ??= now;
    }

    private static bool FollowsOwner(CrewletActivity activity)
    {
        return activity is CrewletActivity.Recall
            or CrewletActivity.CatchUp
            or CrewletActivity.Mourn
            or CrewletActivity.Deliver
            or CrewletActivity.Greet
            or CrewletActivity.Honk
            or CrewletActivity.Follow
            or CrewletActivity.Wander;
    }

    private bool WantsRecall(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        return ent.Comp.ApartSince is { } since
            && now - since >= ent.Comp.ApartTime
            && IsReady(ent.Comp, CrewletActivity.Recall, now);
    }

    private bool WantsCatchUp(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var distance = ent.Comp.OwnerDistance;
        return distance > ent.Comp.CatchUpRange
            && !float.IsPositiveInfinity(distance)
            && IsReady(ent.Comp, CrewletActivity.CatchUp, now);
    }

    private bool WantsFollow(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var distance = ent.Comp.OwnerDistance;
        if (distance <= ent.Comp.FollowCloseRange)
            ent.Comp.Summoned = false;

        var range = ent.Comp.Summoned ? ent.Comp.FollowCloseRange : ent.Comp.FollowRange;
        return distance > range
            && !float.IsPositiveInfinity(distance)
            && IsReady(ent.Comp, CrewletActivity.Follow, now);
    }

    private bool WantsMourn(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        return ent.Comp.KnownOwner is { } owner
            && _mobState.IsDead(owner)
            && !float.IsPositiveInfinity(ent.Comp.OwnerDistance)
            && IsReady(ent.Comp, CrewletActivity.Mourn, now);
    }

    private bool WantsDeliver(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        return ent.Comp.Activity is not (CrewletActivity.Honk or CrewletActivity.Tinker or CrewletActivity.Habit)
            && HeldItem(ent) != null
            && IsReady(ent.Comp, CrewletActivity.Deliver, now);
    }

    private bool WantsGreet(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        return !ent.Comp.Greeted
            && ent.Comp.KnownOwner is { } owner
            && _mobState.IsAlive(owner)
            && !float.IsPositiveInfinity(ent.Comp.OwnerDistance)
            && IsReady(ent.Comp, CrewletActivity.Greet, now);
    }

    private void RunFollow(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.KnownOwner is not { } owner)
        {
            End(ent);
            return;
        }

        if (ent.Comp.Step == CrewletStep.Start)
            Approach(ent, owner, ent.Comp.FollowCloseRange, now);

        switch (Move(ent, now))
        {
            case CrewletMove.Moving:
                return;
            case CrewletMove.Failed:
                Fail(ent, now);
                return;
            default:
                End(ent);
                return;
        }
    }

    private void RunRecall(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.KnownOwner is not { } owner)
        {
            End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Start)
        {
            Say(ent, Loc.GetString(brain.RecallLine, ("name", CrewletSystem.GetShortName(Name(owner)))));
            SpawnAtPosition(brain.PortalEffect, Transform(ent).Coordinates);

            var landing = ToGridCoordinates(_transform.GetMapCoordinates(owner));
            SpawnAtPosition(brain.PortalEffect, landing);
            _popup.PopupEntity(Loc.GetString(brain.DreadText), owner, owner, PopupType.MediumCaution);

            brain.Destination = landing;
            WaitFor(ent, brain.RecallDelay, now);
            return;
        }

        if (now < brain.StepEnds)
            return;

        if (!_container.IsEntityInContainer(ent) && brain.Destination.IsValid(EntityManager))
        {
            SpawnAtPosition(brain.PortalGulpEffect, Transform(ent).Coordinates);
            _transform.SetCoordinates(ent, brain.Destination);
            _transform.AttachToGridOrMap(ent);
            SpawnAtPosition(brain.PortalExitEffect, brain.Destination);
        }

        brain.ApartSince = null;
        End(ent);
    }

    private void RunMourn(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.KnownOwner is not { } owner || !_mobState.IsDead(owner))
        {
            End(ent);
            return;
        }

        switch (brain.Step)
        {
            case CrewletStep.Start:
                Approach(ent, owner, brain.MeleeRange, now);
                return;
            case CrewletStep.Approach:
                var move = Move(ent, now);
                if (move == CrewletMove.Moving)
                    return;

                if (move != CrewletMove.Arrived)
                {
                    Fail(ent, now);
                    return;
                }

                var lines = _proto.Index(brain.MournLines).Values;
                var name = CrewletSystem.GetShortName(Name(owner));
                Say(ent, Loc.GetString(lines[_random.Next(lines.Count)], ("name", name)));
                _audio.PlayPvs(brain.MournSound, ent);
                WaitFor(ent, _random.Next(brain.MinMournInterval, brain.MaxMournInterval), now);
                return;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                    Approach(ent, owner, brain.MeleeRange, now);
                return;
        }
    }

    private void RunDeliver(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (HeldItem(ent) == null)
        {
            brain.GiftRecipient = null;
            End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Start)
        {
            if (!Present(brain.GiftRecipient))
                brain.GiftRecipient = brain.KnownOwner;

            if (brain.GiftRecipient is { } recipient)
                SetCourse(ent, new(recipient, default), brain.MeleeRange, brain.DeliverTimeout, now);
            else
                WaitFor(ent, brain.DeliverTimeout, now);

            return;
        }

        if (brain.Step == CrewletStep.Wait)
        {
            if (now < brain.StepEnds)
                return;

            _hands.TryDrop(ent.Owner);
            if (HeldItem(ent) != null)
                Fail(ent, now);
            else
                End(ent);
            return;
        }

        var move = Move(ent, now);
        if (move == CrewletMove.Moving)
            return;

        if (move == CrewletMove.Arrived && brain.GiftRecipient is { } receiver && Present(receiver))
            _hands.TryDrop(ent.Owner, Transform(receiver).Coordinates);
        else
            _hands.TryDrop(ent.Owner);

        brain.GiftRecipient = null;
        if (HeldItem(ent) != null)
            Fail(ent, now);
        else
            End(ent);
    }

    private EntityUid? PickRecipient(Entity<CrewletBrainComponent> ent)
    {
        var brain = ent.Comp;
        TryComp<CrewletReputationComponent>(ent, out var reputation);

        var total = brain.KnownOwner != null ? brain.GiftOwnerWeight : 0f;
        foreach (var person in brain.People)
        {
            total += GiftWeight(ent, reputation, person);
        }

        if (total <= 0f)
            return null;

        var roll = _random.NextFloat() * total;
        if (brain.KnownOwner is { } owner)
        {
            roll -= brain.GiftOwnerWeight;
            if (roll < 0f)
                return owner;
        }

        EntityUid? chosen = brain.KnownOwner;
        foreach (var person in brain.People)
        {
            var weight = GiftWeight(ent, reputation, person);
            if (weight <= 0f)
                continue;

            chosen = person;
            roll -= weight;
            if (roll < 0f)
                break;
        }

        return chosen;
    }

    private float GiftWeight(Entity<CrewletBrainComponent> ent,
        CrewletReputationComponent? reputation,
        EntityUid person)
    {
        if (reputation == null || person == ent.Comp.KnownOwner || !AliveNearby(ent, person))
            return 0f;

        var opinion = _reputation.GetOpinion((ent.Owner, reputation), person);
        return opinion < reputation.FriendlyThreshold ? 0f : opinion / MathF.Max(reputation.FriendlyThreshold, 1f);
    }

    private EntityUid? HeldItem(EntityUid uid)
    {
        return _hands.GetActiveItem(uid) is { } item && !_virtualItemQuery.HasComp(item) ? item : null;
    }

    private void RunGreet(Entity<CrewletBrainComponent> ent, TimeSpan now)
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
                brain.Greeted = true;
                Approach(ent, owner, brain.MeleeRange, now);
                return;
            case CrewletStep.Approach:
                var move = Move(ent, now);
                if (move == CrewletMove.Moving)
                    return;

                if (move != CrewletMove.Arrived)
                {
                    End(ent);
                    return;
                }

                Interact(ent, owner);
                WaitFor(ent, brain.HugRest, now);
                return;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                    End(ent);
                return;
        }
    }
}

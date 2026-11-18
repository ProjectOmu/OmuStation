// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Crewlet;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private void OnOwnerUnequipped(Entity<CrewletOwnerComponent> ent, ref DidUnequipEvent args)
    {
        foreach (var uid in ent.Comp.Crewlets)
        {
            if (TryComp<CrewletBrainComponent>(uid, out var brain) && args.Slot == brain.HatSlot)
                brain.OwnerHat = args.Equipment;
        }
    }

    private void RunHat(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Step == CrewletStep.Start)
        {
            if (HeldItem(ent) != null || !FreeSlot(ent, brain.HatSlot))
            {
                End(ent);
                return;
            }

            if (!Begin(ent, PickHat(ent, now), brain.MeleeRange, now))
                return;
        }

        if (!Reach(ent, now, out var hat))
            return;

        Interact(ent, hat);
        if (_hands.IsHolding(ent.Owner, hat))
            _inventory.TryEquip(ent, hat, brain.HatSlot, silent: true);

        End(ent);
    }

    private EntityUid? PickHat(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.OwnerHat is { } owned
            && IsCandidate(ent, owned, CrewletPick.Hat, now)
            && DistanceTo(ent, owned) <= ent.Comp.VisionRadius
            && InSight(ent, owned))
            return owned;

        return PickNearest(ent, ent.Comp.Items, CrewletPick.Hat, now);
    }

    private bool FreeSlot(EntityUid uid, string slot)
    {
        return _inventory.HasSlot(uid, slot) && !_inventory.TryGetSlotEntity(uid, slot, out _);
    }

    private bool Fits(EntityUid uid, EntityUid item, string slot)
    {
        return TryComp<ClothingComponent>(item, out var clothing)
            && _inventory.TryGetSlot(uid, slot, out var definition)
            && (clothing.Slots & definition.SlotFlags) != 0;
    }

    private void RunPickpocket(Entity<CrewletBrainComponent> ent, TimeSpan now)
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

                Begin(ent, PickMark(ent, now), brain.MeleeRange, now);
                return;
            case CrewletStep.Approach:
            case CrewletStep.Act:
                if (!Reach(ent, now, out var victim))
                    return;

                if (!StartPickpocket(ent, victim))
                {
                    End(ent);
                    return;
                }

                WaitFor(ent, brain.PickpocketTime + DoAfterGrace, now);
                return;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                    End(ent);
                return;
        }
    }

    private EntityUid? PickMark(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (!TryComp<CrewletReputationComponent>(ent, out var reputation))
            return null;

        EntityUid? best = null;
        string? bestSlot = null;
        var bestDistance = brain.VisionRadius;
        foreach (var person in brain.People)
        {
            if (!TargetReady(brain, person, now)
                || !Disliked(ent, reputation, person)
                || Stealable(ent, person) is not { } slot)
                continue;

            var distance = DistanceTo(ent, person);
            if (distance > bestDistance)
                continue;

            best = person;
            bestSlot = slot;
            bestDistance = distance;
        }

        if (best is not { } mark || !InSight(ent, mark))
            return null;

        brain.PickpocketSlot = bestSlot;
        return mark;
    }

    private string? Stealable(Entity<CrewletBrainComponent> ent, EntityUid person)
    {
        foreach (var slot in ent.Comp.PickpocketSlots)
        {
            if (FreeSlot(ent, slot)
                && _inventory.TryGetSlotEntity(person, slot, out var item)
                && !_whitelist.IsWhitelistPass(ent.Comp.PickpocketBlacklist, item.Value)
                && Fits(ent, item.Value, slot))
                return slot;
        }

        return null;
    }

    private bool StartPickpocket(Entity<CrewletBrainComponent> ent, EntityUid victim)
    {
        if (ent.Comp.PickpocketSlot is not { } slot || !_inventory.TryGetSlotEntity(victim, slot, out var item))
            return false;

        var args = new DoAfterArgs(EntityManager,
            ent,
            ent.Comp.PickpocketTime,
            new CrewletPickpocketDoAfterEvent(),
            ent,
            target: victim,
            used: item)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = ent.Comp.InteractRange,
        };

        if (!_doAfter.TryStartDoAfter(args, out ent.Comp.ActivityDoAfter))
            return false;

        _popup.PopupEntity(Loc.GetString(ent.Comp.PickpocketText, ("crewlet", ent.Owner), ("item", item.Value)),
            victim,
            victim,
            PopupType.SmallCaution);
        return true;
    }

    private void OnPickpocketDoAfter(Entity<CrewletBrainComponent> ent, ref CrewletPickpocketDoAfterEvent args)
    {
        ent.Comp.ActivityDoAfter = null;
        if (args.Handled
            || args.Cancelled
            || args.Target is not { } victim
            || args.Used is not { } item
            || ent.Comp.PickpocketSlot is not { } slot
            || !_inventory.TryGetSlotEntity(victim, slot, out var worn)
            || worn != item)
            return;

        args.Handled = true;
        if (!_inventory.TryUnequip(ent, victim, slot, silent: true)
            || !_hands.TryPickupAnyHand(ent, item))
            return;

        _inventory.TryEquip(ent, item, slot, silent: true);
    }
}

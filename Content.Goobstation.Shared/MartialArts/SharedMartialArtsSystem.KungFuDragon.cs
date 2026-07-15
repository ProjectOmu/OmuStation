// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <aviu00@protonmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts; // Omu
using Content.Goobstation.Maths.FixedPoint; // Omu
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Clothing; // Omu
using Content.Shared.Damage; // Omu
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee; // Omu
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeDragon()
    {
        SubscribeLocalEvent<CanPerformComboComponent, DragonClawPerformedEvent>(OnDragonClaw);
        SubscribeLocalEvent<CanPerformComboComponent, DragonTailPerformedEvent>(OnDragonTail);
        SubscribeLocalEvent<CanPerformComboComponent, DragonStrikePerformedEvent>(OnDragonStrike);

        SubscribeLocalEvent<GrantKungFuDragonComponent, UseInHandEvent>(OnGrantCQCUse);

        // Omu start
        SubscribeLocalEvent<GrantKungFuDragonComponent, ClothingGotEquippedEvent>(OnWear);
        SubscribeLocalEvent<GrantKungFuDragonComponent, ClothingGotUnequippedEvent>(OnRemove);
        // Omu end

        SubscribeLocalEvent<DragonPowerBuffComponent, AttackedEvent>(OnAttacked);
    }

    // Omu start
    private void OnWear(EntityUid ent, GrantKungFuDragonComponent component, ref ClothingGotEquippedEvent args)
    {
        if (!_netManager.IsServer)
            return;

        var user = args.Wearer;
        TryGrantMartialArt(user, component);
    }

    private void OnRemove(Entity<GrantKungFuDragonComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        var user = args.Wearer;

        // Omu Station
        // Don't proceed if the user has non-removable Martial Arts knowledge
        if (HasManualCqcKnowledge(user))
            return;

        if (!TryComp<MartialArtsKnowledgeComponent>(user, out var martialartsknowcomp)
            || !TryComp<MeleeWeaponComponent>(user, out var meleeweaponcomp))
            return;

        if (martialartsknowcomp.MartialArtsForm != MartialArtsForms.KungFuDragon)
            return;

        var originalDamage = new DamageSpecifier();
        originalDamage.DamageDict[martialartsknowcomp.OriginalFistDamageType]
            = FixedPoint2.New(martialartsknowcomp.OriginalFistDamage);
        meleeweaponcomp.Damage = originalDamage;

        RemComp<MartialArtsKnowledgeComponent>(user);
        RemComp<CanPerformComboComponent>(user);
    }
    // Omu end

    private void OnAttacked(Entity<DragonPowerBuffComponent> ent, ref AttackedEvent args)
    {
        if (_hands.TryGetActiveItem(ent.Owner, out _) // Only unarmed
            || !_blocker.CanInteract(ent, null)) // Should be able to interact
            return;

        args.ModifiersList.Add(ent.Comp.ModifierSet);

        // Works for both armed and unarmed attacks
        ApplyMultiplier(ent,
            ent.Comp.DamageMultiplier,
            0f,
            ent.Comp.AttackDamageBuffDuration,
            MartialArtModifierType.Damage);
    }

    private void OnDragonStrike(Entity<CanPerformComboComponent> ent, ref DragonStrikePerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed))
            return;

        if (!downed)
        {
            _popupSystem.PopupEntity(Loc.GetString("martial-arts-fail-target-standing"), ent, ent);
            return;
        }

        // Paralyze, not knockdown
        _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(proto.ParalyzeTime));
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);
        _audio.PlayPvs(args.Sound, target);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnDragonTail(Entity<CanPerformComboComponent> ent, ref DragonTailPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed))
            return;

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        if (downed)
            _stun.TryUpdateStunDuration(target, args.DownedParalyzeTime); // No stunlocks
        else
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, applyResistances: true);
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(proto.ParalyzeTime), true, true, proto.DropItems);
            DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);
        }

        _audio.PlayPvs(args.Sound, target);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();
    }


    private void OnDragonClaw(Entity<CanPerformComboComponent> ent, ref DragonClawPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out _))
            return;
        _movementMod.TryUpdateMovementSpeedModDuration(target, MartsGenericSlow, args.SlowdownTime, args.WalkSpeedModifier, args.SprintSpeedModifier);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, applyResistances: true);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);
        _audio.PlayPvs(args.Sound, target);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();
    }
}

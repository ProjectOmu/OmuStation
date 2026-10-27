// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Grab;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.GrabIntent;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private const int WrestlingLongNameLength = 12;

    private void InitializeWrestling()
    {
        SubscribeLocalEvent<CanPerformComboComponent, WrestlingSuplexPerformedEvent>(OnWrestlingSuplex);
        SubscribeLocalEvent<CanPerformComboComponent, WrestlingRkoPerformedEvent>(OnWrestlingRko);
        SubscribeLocalEvent<CanPerformComboComponent, WrestlingDdtPerformedEvent>(OnWrestlingDdt);
        SubscribeLocalEvent<CanPerformComboComponent, WrestlingElbowDropPerformedEvent>(OnWrestlingElbowDrop);
        SubscribeLocalEvent<CanPerformComboComponent, WrestlingSubmissionPerformedEvent>(OnWrestlingSubmission);
        SubscribeLocalEvent<CanPerformComboComponent, WrestlingDropkickPerformedEvent>(OnWrestlingDropkick);
        SubscribeLocalEvent<CanPerformComboComponent, WrestlingPowerbombPerformedEvent>(OnWrestlingPowerbomb);

        SubscribeLocalEvent<GrantWrestlingComponent, ClothingGotEquippedEvent>(OnWear);
        SubscribeLocalEvent<GrantWrestlingComponent, ClothingGotUnequippedEvent>(OnRemove);
    }

    private void OnWear(EntityUid uid, GrantWrestlingComponent component, ref ClothingGotEquippedEvent args)
    {
        if (!_netManager.IsServer
            || !HasComp<TargetingComponent>(args.Wearer)
            || !TryGrantMartialArt(args.Wearer, component))
            return;

        if (component.EquipSound is { } cheer)
            RaiseNetworkEvent(new WrestlingBeltCheerEvent(cheer), Filter.Entities(args.Wearer));
    }

    private bool CheckTitleMatch(EntityUid wrestler, EntityUid opponent)
    {
        if (!_inventory.TryGetSlotEntity(wrestler, "belt", out var belt)
            || !TryComp<GrantWrestlingComponent>(belt, out var grant)
            || !grant.RequireOpponentBelt)
            return true;

        if (_inventory.TryGetSlotEntity(opponent, "belt", out var opponentBelt)
            && HasComp<GrantWrestlingComponent>(opponentBelt))
            return true;

        _popupSystem.PopupEntity(Loc.GetString("wrestling-fail-not-title-match"), wrestler, wrestler);
        return false;
    }

    private void OnRemove(Entity<GrantWrestlingComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        var user = args.Wearer;

        if (HasManualCqcKnowledge(user)
            || !TryComp<MartialArtsKnowledgeComponent>(user, out var knowledge)
            || !TryComp<MeleeWeaponComponent>(user, out var melee)
            || knowledge.MartialArtsForm != MartialArtsForms.Wrestling)
            return;

        var originalDamage = new DamageSpecifier();
        originalDamage.DamageDict[knowledge.OriginalFistDamageType] = FixedPoint2.New(knowledge.OriginalFistDamage);
        melee.Damage = originalDamage;

        RemComp<MartialArtsKnowledgeComponent>(user);
        RemComp<CanPerformComboComponent>(user);
    }

    private void OnWrestlingSuplex(Entity<CanPerformComboComponent> ent, ref WrestlingSuplexPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !CheckTitleMatch(ent, target))
            return;

        if (downed)
        {
            _popupSystem.PopupEntity(Loc.GetString("martial-arts-fail-target-down"), ent, ent);
            return;
        }

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        var overShoulder = _transform.GetMapCoordinates(ent).Position - _transform.GetMapCoordinates(target).Position;
        var distance = overShoulder.Length();
        if (distance > 0f)
        {
            var landing = overShoulder * ((distance + args.ThrowDistance) / distance);
            _grabThrowing.Throw(target, ent, landing, proto.ThrownSpeed, behavior: proto.DropItems);
        }

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();

        if (!_netManager.IsServer)
            return;

        var landedDown = _standingState.IsDown(target);
        var suplex = new WrestlingSuplexAnimationEvent(GetNetEntity(target), args.Sound, landedDown);
        RaiseNetworkEvent(suplex, Filter.Pvs(target, entityManager: EntityManager));
    }

    private void OnWrestlingRko(Entity<CanPerformComboComponent> ent, ref WrestlingRkoPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !CheckTitleMatch(ent, target))
            return;

        if (downed)
        {
            _popupSystem.PopupEntity(Loc.GetString("martial-arts-fail-target-down"), ent, ent);
            return;
        }

        if (GetVelocity(ent) < args.MinVelocity)
        {
            _popupSystem.PopupEntity(Loc.GetString("wrestling-fail-standing-still"), ent, ent);
            return;
        }

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Head);

        var toTarget = _transform.GetMapCoordinates(target).Position - _transform.GetMapCoordinates(ent).Position;
        var distance = toTarget.Length();
        if (distance > args.LandShortOfTarget)
        {
            var lunge = toTarget * ((distance - args.LandShortOfTarget) / distance);
            _grabThrowing.Throw(ent, target, lunge, proto.ThrownSpeed);
        }

        _stun.TryKnockdown(ent.Owner, args.SelfKnockdownTime, true, true, false);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();

        if (!_netManager.IsServer)
            return;

        var rko = new WrestlingRkoAnimationEvent(GetNetEntity(ent),
            GetNetEntity(target),
            args.Sound,
            args.CrowdSound,
            _standingState.IsDown(target));
        RaiseNetworkEvent(rko, Filter.Pvs(target, entityManager: EntityManager));

        if (args.Catchphrase is { } catchphrase)
            SayWrestlingCatchphrase(ent, catchphrase);
    }

    private void OnWrestlingDdt(Entity<CanPerformComboComponent> ent, ref WrestlingDdtPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !CheckTitleMatch(ent, target))
            return;

        if (downed)
        {
            DoDamage(ent, target, proto.DamageType, proto.ExtraDamage / 2, out _, TargetBodyPart.Head);
            _hands.TryDrop(target);
        }
        else
        {
            DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Head);
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
            _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
            _stun.TryKnockdown(ent.Owner, args.SelfKnockdownTime, true, true, false);
        }

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();

        if (!_netManager.IsServer)
            return;

        var ddt = new WrestlingDdtAnimationEvent(GetNetEntity(target),
            args.Sound,
            !downed,
            _standingState.IsDown(target));
        RaiseNetworkEvent(ddt, Filter.Pvs(target, entityManager: EntityManager));
    }

    private void OnWrestlingElbowDrop(Entity<CanPerformComboComponent> ent, ref WrestlingElbowDropPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !CheckTitleMatch(ent, target))
            return;

        if (!downed)
        {
            _popupSystem.PopupEntity(Loc.GetString("martial-arts-fail-target-standing"), ent, ent);
            return;
        }

        _stun.TryUpdateParalyzeDuration(target, proto.ParalyzeTime);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Chest);

        var toTarget = _transform.GetMapCoordinates(target).Position - _transform.GetMapCoordinates(ent).Position;
        if (toTarget.LengthSquared() > 0f)
            _grabThrowing.Throw(ent, target, toTarget, proto.ThrownSpeed);

        _stun.TryKnockdown(ent.Owner, args.SelfKnockdownTime, true, true, false);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();

        if (!_netManager.IsServer)
            return;

        var elbowDrop = new WrestlingElbowDropAnimationEvent(GetNetEntity(ent), GetNetEntity(target), args.Sound);
        RaiseNetworkEvent(elbowDrop, Filter.Pvs(target, entityManager: EntityManager));
    }

    private void OnWrestlingSubmission(Entity<CanPerformComboComponent> ent, ref WrestlingSubmissionPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !CheckTitleMatch(ent, target)
            || !downed
            || !TryComp<PullerComponent>(ent, out var puller)
            || !TryComp<GrabIntentComponent>(ent, out var grabIntent)
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<GrabbableComponent>(target, out var grabbable))
            return;

        var holdTime = proto.ParalyzeTime;
        var resist = new BeforeStaminaDamageEvent(1f);
        RaiseLocalEvent(target, ref resist);
        holdTime *= resist.Value;

        if (!HasComp<ArmbarredComponent>(target))
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage);
            AddComp<ArmbarredComponent>(target).Puller = ent;
        }

        if (grabIntent.GrabStage != GrabStage.Suffocate || grabbable.GrabStage != GrabStage.Suffocate)
            _grab.TrySetGrabStages((ent, puller, grabIntent), (target, pullable, grabbable), GrabStage.Suffocate);

        _stun.TryKnockdown(target, holdTime, true, true, proto.DropItems);
        _audio.PlayPvs(args.Sound, target);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();
    }

    private void OnWrestlingDropkick(Entity<CanPerformComboComponent> ent, ref WrestlingDropkickPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !CheckTitleMatch(ent, target))
            return;

        if (downed)
        {
            _popupSystem.PopupEntity(Loc.GetString("martial-arts-fail-target-down"), ent, ent);
            return;
        }

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Chest);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);

        var away = _transform.GetMapCoordinates(target).Position - _transform.GetMapCoordinates(ent).Position;
        var distance = away.Length();
        if (distance > 0f)
        {
            var knockback = away * (args.KnockbackDistance / distance);
            _grabThrowing.Throw(target, ent, knockback, proto.ThrownSpeed, behavior: proto.DropItems);
        }

        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        _stun.TryKnockdown(ent.Owner, args.SelfKnockdownTime, true, true, false);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();

        if (!_netManager.IsServer)
            return;

        var dropkick = new WrestlingDropkickAnimationEvent(GetNetEntity(ent), GetNetEntity(target), args.Sound);
        RaiseNetworkEvent(dropkick, Filter.Pvs(target, entityManager: EntityManager));
    }

    private void OnWrestlingPowerbomb(Entity<CanPerformComboComponent> ent, ref WrestlingPowerbombPerformedEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.BeingPerformed, out var proto)
            || !TryUseMartialArt(ent, proto, out var target, out var downed)
            || !CheckTitleMatch(ent, target)
            || !downed
            || !TryComp<ArmbarredComponent>(target, out var armbarred)
            || armbarred.Puller != ent.Owner)
            return;

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, ent, true);

        var wasCritical = _mobState.IsCritical(target);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, out _, TargetBodyPart.Chest);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();

        if (!_netManager.IsServer)
            return;

        var critSound = !wasCritical && _mobState.IsCritical(target) ? args.CritSound : null;
        var slam = new WrestlingSlamAnimationEvent(GetNetEntity(target),
            args.Sound,
            args.CrowdSound,
            args.BellSound,
            critSound);
        RaiseNetworkEvent(slam, Filter.Pvs(target, entityManager: EntityManager));

        if (!_proto.TryIndex<MartialArtPrototype>(proto.MartialArtsForm.ToString(), out var style)
            || style.RandomSayings.Count == 0)
            return;

        _inventory.TryGetSlotEntity(ent, "belt", out var belt);
        TryComp<GrantWrestlingComponent>(belt, out var grant);

        var fresh = style.RandomSayings.Where(line => line != grant?.LastSaying).ToList();
        var saying = _random.Pick(fresh.Count > 0 ? fresh : style.RandomSayings);
        if (grant != null)
            grant.LastSaying = saying;

        SayWrestlingCatchphrase(ent, saying);
    }

    private void SayWrestlingCatchphrase(EntityUid wrestler, LocId line)
    {
        var fullName = Identity.Name(wrestler, EntityManager).ToUpperInvariant();
        var name = fullName.Length > WrestlingLongNameLength ? fullName.Split(' ', 2)[0] : fullName;
        var oneWord = name.Contains(' ') ? "no" : "yes";

        var catchphrase = Loc.GetString(line, ("name", name), ("fullName", fullName), ("oneWord", oneWord));
        RaiseLocalEvent(wrestler, new WrestlingCatchphraseEvent(catchphrase));
    }
}

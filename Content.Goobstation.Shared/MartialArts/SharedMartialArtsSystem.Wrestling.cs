// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Common.Grab;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Common.SecondSkin;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.GrabIntent;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Goobstation.Shared.Sprinting;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Destructible;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private const int WrestlingLongNameLength = 12;

    private static readonly ProtoId<TraumaTypePrototype>[] WrestlingSafeTraumas = ["BoneDamage", "OrganDamage"];

    [Dependency] private readonly SharedDestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

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

        SubscribeLocalEvent<WrestlingHitComponent, GetSecondSkinDeductionEvent>(OnWrestlingHitDeduction);
        SubscribeLocalEvent<DropkickedComponent, StartCollideEvent>(OnDropkickedCollide);
    }

    private void OnWrestlingHitDeduction(Entity<WrestlingHitComponent> ent, ref GetSecondSkinDeductionEvent args)
    {
        if (WrestlingSafeTraumas.Contains(args.TraumaType))
            args.Deduction = 1f;
    }

    private void DoWrestlingDamage(EntityUid ent,
        EntityUid target,
        string damageType,
        float damageAmount,
        TargetBodyPart? targetBodyPart = null)
    {
        EnsureComp<WrestlingHitComponent>(target);
        DoDamage(ent, target, damageType, damageAmount, out _, targetBodyPart);
        RemComp<WrestlingHitComponent>(target);
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

        DoWrestlingDamage(ent, target, proto.DamageType, proto.ExtraDamage);
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
        DoWrestlingDamage(ent, target, proto.DamageType, proto.ExtraDamage, TargetBodyPart.Head);

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
            DoWrestlingDamage(ent, target, proto.DamageType, proto.ExtraDamage / 2, TargetBodyPart.Head);
            _hands.TryDrop(target);
        }
        else
        {
            DoWrestlingDamage(ent, target, proto.DamageType, proto.ExtraDamage, TargetBodyPart.Head);
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
        DoWrestlingDamage(ent, target, proto.DamageType, proto.ExtraDamage, TargetBodyPart.Chest);

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

        if (_timing.CurTime < ent.Comp.NextSubmission)
        {
            _popupSystem.PopupEntity(Loc.GetString("wrestling-fail-submission-cooldown"), ent, ent);
            return;
        }

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
        if (!TryComp<SprinterComponent>(ent, out var sprinter) || !sprinter.IsSprinting)
            return;

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

        DoWrestlingDamage(ent, target, proto.DamageType, proto.ExtraDamage, TargetBodyPart.Chest);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);

        var smashedThrough = false;
        var away = _transform.GetMapCoordinates(target).Position - _transform.GetMapCoordinates(ent).Position;
        var distance = away.Length();
        if (distance > 0f)
        {
            var knockback = away * (args.KnockbackDistance / distance);
            smashedThrough = SmashThroughWindows(target, knockback, args);
            _grabThrowing.Throw(target, ent, knockback, proto.ThrownSpeed, behavior: proto.DropItems);
        }

        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        _stun.TryKnockdown(ent.Owner, args.SelfKnockdownTime, true, true, false);
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();

        if (!_netManager.IsServer)
            return;

        if (smashedThrough)
        {
            _audio.PlayPvs(args.BreakThroughSound, target);
        }
        else
        {
            var dropkicked = EnsureComp<DropkickedComponent>(target);
            dropkicked.BreakThroughEnd = _timing.CurTime + args.BreakThroughTime;
            dropkicked.BreakableWindows = args.BreakableWindows;
            dropkicked.BreakableGrilles = args.BreakableGrilles;
            dropkicked.BreakThroughSound = args.BreakThroughSound;
        }

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
        DoWrestlingDamage(ent, target, proto.DamageType, proto.ExtraDamage, TargetBodyPart.Chest);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        _stun.TryKnockdown(target, proto.ParalyzeTime, true, true, proto.DropItems);
        ent.Comp.NextSubmission = _timing.CurTime + args.SubmissionCooldown;
        ComboPopup(ent, target, proto.Name);
        ent.Comp.LastAttacks.Clear();

        if (!_netManager.IsServer)
            return;

        var powerbombed = EnsureComp<PowerbombedComponent>(target);
        powerbombed.SlamTime = _timing.CurTime + args.SlamDelay;
        powerbombed.BreakableTables = args.BreakableTables;

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

    private void UpdateWrestling()
    {
        var query = EntityQueryEnumerator<PowerbombedComponent>();
        while (query.MoveNext(out var ent, out var comp))
        {
            if (_timing.CurTime < comp.SlamTime)
                continue;

            var landing = _transform.GetMapCoordinates(ent);
            foreach (var table in _lookup.GetEntitiesIntersecting(landing, LookupFlags.Static))
            {
                if (IsPrototypeIn(table, comp.BreakableTables))
                    SmashWrestlingProp(table);
            }

            RemCompDeferred(ent, comp);
        }

        var dropkickedQuery = EntityQueryEnumerator<DropkickedComponent>();
        while (dropkickedQuery.MoveNext(out var ent, out var comp))
        {
            if (_timing.CurTime < comp.BreakThroughEnd)
                continue;

            RemCompDeferred(ent, comp);
        }
    }

    private void OnDropkickedCollide(Entity<DropkickedComponent> ent, ref StartCollideEvent args)
    {
        if (!SmashWindowTile(args.OtherEntity, ent.Comp.BreakableWindows, ent.Comp.BreakableGrilles))
            return;

        _audio.PlayPvs(ent.Comp.BreakThroughSound, ent);
        RemCompDeferred(ent, ent.Comp);
    }

    private bool SmashThroughWindows(EntityUid target, Vector2 knockback, WrestlingDropkickPerformedEvent args)
    {
        var start = _transform.GetMapCoordinates(target);
        var path = Box2.FromTwoPoints(start.Position, start.Position + knockback).Enlarged(0.4f);

        var smashed = false;
        foreach (var hit in _lookup.GetEntitiesIntersecting(start.MapId, path, LookupFlags.Static))
            smashed |= SmashWindowTile(hit, args.BreakableWindows, args.BreakableGrilles);

        return smashed;
    }

    private bool SmashWindowTile(EntityUid hit, List<EntProtoId> windows, List<EntProtoId> grilles)
    {
        if (_transform.GetGrid(hit) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.GetAnchoredEntities((grid, gridComp), _transform.GetMapCoordinates(hit)).ToList();
        if (!tile.Any(uid => IsPrototypeIn(uid, windows)))
            return false;

        foreach (var uid in tile)
        {
            if (IsPrototypeIn(uid, windows) || IsPrototypeIn(uid, grilles))
                SmashWrestlingProp(uid);
        }

        return true;
    }

    private void SmashWrestlingProp(EntityUid prop)
    {
        var damageNeeded = _destructible.GetDestroyedAt(prop);
        if (damageNeeded == FixedPoint2.MaxValue)
            return;

        if (TryComp<DamageableComponent>(prop, out var damageable))
            damageNeeded -= damageable.TotalDamage;

        if (damageNeeded <= FixedPoint2.Zero)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", damageNeeded);
        _damageable.TryChangeDamage(prop, damage, true);
    }

    private bool IsPrototypeIn(EntityUid uid, List<EntProtoId> ids)
    {
        return Prototype(uid) is { } proto && ids.Contains(proto.ID);
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

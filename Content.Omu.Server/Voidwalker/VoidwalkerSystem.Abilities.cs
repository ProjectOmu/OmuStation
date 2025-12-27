using System.Collections.Immutable;
using Content.Omu.Server.Voidwalker.Kidnapping;
using Content.Omu.Server.Voidwalker.Kidnapping.Voided;
using Content.Omu.Server.Voidwalker.Objectives.Components;
using Content.Omu.Shared.Voidwalker;
using Content.Omu.Shared.Voidwalker.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Omu.Server.Voidwalker;

public sealed partial class VoidwalkerSystem
{
    private static ProtoId<EmotePrototype> Scream = "Scream";
    private void SubscribeAbilities()
    {
        SubscribeLocalEvent<VoidwalkerComponent, VoidwalkerUnsettleEvent>(OnUnsettle);
        SubscribeLocalEvent<VoidwalkerComponent, VoidwalkerUnsettleDoAfterEvent>(OnUnsettleDoAfter);

        SubscribeLocalEvent<VoidwalkerComponent, VoidwalkerKidnapDoAfterEvent>(OnVoidwalkerKidnapDoAfter);
    }

    private void OnUnsettle(Entity<VoidwalkerComponent> entity, ref VoidwalkerUnsettleEvent args)
    {
        var target = args.Target;

        if (_mobState.IsIncapacitated(target))
        {
            _popup.PopupEntity(Loc.GetString("voidwalker-unsettle-fail-incapacitated"), target, entity);
            return;
        }

        if (!CanSeeVoidwalker(target))
        {
            _popup.PopupEntity(Loc.GetString("voidwalker-unsettle-fail-blind"), target, entity);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            entity,
            entity.Comp.UnsettleDoAfterDuration,
            new VoidwalkerUnsettleDoAfterEvent(),
            eventTarget: entity,
            target: target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            RequireCanInteract = false, // use your EYES!!!!
            IgnoreObstruction = true,
            DistanceThreshold = 20,
            ShowTo = entity,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var id))
            return;

        entity.Comp.UnsettleDoAfterId = id.Value.Index;

        var popup = Loc.GetString("voidwalker-unsettle-begin", ("target", Name(target)));
        _popup.PopupEntity(popup, entity, entity, PopupType.Medium);
    }

    private void OnUnsettleDoAfter(Entity<VoidwalkerComponent> entity, ref VoidwalkerUnsettleDoAfterEvent args)
    {
        entity.Comp.UnsettleDoAfterId = null;

        if (args.Target is not { } target
            || args.Cancelled
            || args.Handled)
            return;

        args.Handled = true;

        _stun.KnockdownOrStun(target, entity.Comp.UnsettleStunDuration, true);
        _chat.TryEmoteWithChat(target, Scream);
        _stamina.TakeStaminaDamage(target, entity.Comp.UnsettleStaminaDamage);
        _slurred.DoSlur(target, entity.Comp.UnsettleStunDuration * 2);

        var popup = Loc.GetString("voidwalker-unsettle-victim");
        _popup.PopupEntity(popup, target, target, PopupType.LargeCaution);
    }

    private void StartKidnap(Entity<VoidwalkerComponent> entity, EntityUid target)
    {
        var popup = Loc.GetString("voidwalker-kidnap-begin", ("target", Name(target)), ("user", Name(entity.Owner)));
        _popup.PopupEntity(popup, target, PopupType.MediumCaution);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            entity,
            entity.Comp.KidnapDoAfterDuration,
            new VoidwalkerKidnapDoAfterEvent(),
            eventTarget: entity,
            target: target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnVoidwalkerKidnapDoAfter(Entity<VoidwalkerComponent> entity, ref VoidwalkerKidnapDoAfterEvent args)
    {
        if (args.Target is not { } target
            || args.Cancelled
            || args.Handled)
            return;

        args.Handled = true;

        if (_mind.TryGetMind(entity, out var voidwalkerMindId, out var voidwalkerMind)
            && _mind.TryGetObjectiveComp<VoidwalkerKidnapConditionComponent>(voidwalkerMindId, out var objective, voidwalkerMind))
            objective.Kidnapped += 1;

        if (!TryComp<MindContainerComponent>(target, out var targetMindContainer)
            || !targetMindContainer.HasMind)
            return;

        var originalMapId = Transform(target).MapID;
        var originalMapUid = _map.GetMap(originalMapId);

        var targetMindEntity = targetMindContainer.Mind.Value;
        var targetMind = Comp<MindComponent>(targetMindEntity);
        targetMind.PreventGhosting = true;

        var spawnPoints = EntityManager
            .GetAllComponents(typeof(VoidedSpawnComponent))
            .ToImmutableList();

        if (spawnPoints.IsEmpty)
            return;

        var newSpawn = _random.Pick(spawnPoints);
        var spawnTarget = Transform(newSpawn.Uid).Coordinates;

        _transform.SetCoordinates(target, spawnTarget);
        _rejuvenate.PerformRejuvenate(target);
        _stun.KnockdownOrStun(target, entity.Comp.UnsettleStunDuration, true); // doesn't really matter how long this lasts so uh.. tie it to that idk :shrug:
        // need more sfx here later

        var kidnappedComp = EnsureComp<VoidwalkerKidnappedComponent>(target);
        kidnappedComp.ExitVoidTime = _timing.CurTime + entity.Comp.KidnapDuration;
        kidnappedComp.OriginalMap = originalMapUid;

        var voidedComp = EnsureComp<VoidedComponent>(target);
        voidedComp.Voidwalker = entity;
    }

}

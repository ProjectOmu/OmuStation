using Content.Omu.Shared.Voidwalker;
using Content.Omu.Shared.Voidwalker.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Voidwalker;

public sealed partial class VoidwalkerSystem
{
    private static ProtoId<EmotePrototype> Scream = "Scream";
    private void SubscribeAbilities()
    {
        SubscribeLocalEvent<VoidwalkerComponent, VoidwalkerUnsettleEvent>(OnUnsettle);
        SubscribeLocalEvent<VoidwalkerComponent, VoidwalkerUnsettleDoAfterEvent>(OnUnsettleDoAfter);
    }

    private void OnUnsettle(Entity<VoidwalkerComponent> entity, ref VoidwalkerUnsettleEvent args)
    {
        var target = args.Target;

        if (_mobState.IsIncapacitated(target))
        {
            _popup.PopupEntity("voidwalker-unsettle-fail-incapacitated", target, entity);
            return;
        }

        if (!CanSeeVoidwalker(target))
        {
            _popup.PopupEntity("voidwalker-unsettle-fail-blind", target, entity);
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

        _stun.KnockdownOrStun(target, entity.Comp.UnsettleStunDuration, true);
        _chat.TryEmoteWithChat(target, Scream);
        _stamina.TakeStaminaDamage(target, entity.Comp.UnsettleStaminaDamage);
        _slurred.DoSlur(target, entity.Comp.UnsettleStunDuration * 2);

        args.Handled = true;

        var popup = Loc.GetString("voidwalker-unsettle-victim");
        _popup.PopupEntity(popup, target, target, PopupType.LargeCaution);
    }

}

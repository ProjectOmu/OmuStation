using Content.Shared.Stunnable;
using Content.Shared.Pulling.Events;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Content.Shared.StatusEffectNew;

namespace Content.Omu.Shared.Stunnable;

// Todo: move this elsewhere with upstream grab refactor. maybe upstream this.
public sealed class OmuSharedStunSystem : EntitySystem
{
    public static readonly EntProtoId StunId = "StatusEffectStunned";
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectNew.StatusEffectsSystem _status = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<StunnedComponent, AttemptStopPullingEvent>(HandleStopPull);
        SubscribeLocalEvent<StunnedComponent, InteractionSuccessEvent>(BreakStunOnShake);
    }
    private void HandleStopPull(EntityUid uid, StunnedComponent _, ref AttemptStopPullingEvent args)
    {
        if (args.User == null || !Exists(args.User.Value))
            return;
        if (args.User.Value == uid)
        {
            //TODO: UX feedback. Simply blocking the normal interaction feels like an interface bug
            args.Cancelled = true;
        }
    }
    private bool TryChangeStunDuration(EntityUid uid, TimeSpan duration)
    {
        return _status.TryAddTime(uid, StunId, duration);
    }
    private void BreakStunOnShake(Entity<StunnedComponent> ent, ref InteractionSuccessEvent args)
    {
        var result = TryChangeStunDuration(ent.Owner, TimeSpan.FromSeconds(-2));

        if (result == true)
        {
            // var msgOthers = Loc.GetString(component.MessagePerceivedByOthers,
            //    ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(uid, EntityManager)));
            _popup.PopupClient(Loc.GetString("shakeable-popup-message-others"), args.User, args.Actor);
            // _popup.PopupEntity(msgOthers, uid, Filter.PvsExcept(user, entityManager: EntityManager), true);
        }
    }
}


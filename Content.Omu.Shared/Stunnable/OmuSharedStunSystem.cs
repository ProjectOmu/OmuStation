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
    // [Dependency] private readonly SharedStunSystem _stun = default!; - holding this for later
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
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
        var result = TryChangeStunDuration(ent.Owner, TimeSpan.FromSeconds(-2)); // TBD: make this customizable on yaml

        if (result != true)
            return;
        _popup.PopupEntity(Loc.GetString("shakeable-popup-message-others", ("user", args.User), ("shakeable", ent.Owner)), args.User, args.User); // Gives everyone around a popup whenever shaken
        _popup.PopupClient(Loc.GetString("shakeable-popup-message-self", ("user", ent.Owner)), ent.Owner); // Gives the person who is shaking a popup whenever doing so

    }
}


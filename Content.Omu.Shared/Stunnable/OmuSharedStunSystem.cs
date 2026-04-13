using Content.Shared.Stunnable;
using Content.Shared.Pulling.Events;

namespace Content.Omu.Shared.Stunnable;

// Todo: move this elsewhere with upstream grab refactor. maybe upstream this.
public sealed class OmuSharedStunSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<StunnedComponent, AttemptStopPullingEvent>(HandleStopPull);
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
}

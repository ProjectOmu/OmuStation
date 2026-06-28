using Content.Shared.Actions;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Mech.EntitySystems;

namespace Content.Shared._Omu.Entities.Objects.BloodredVim;

public abstract partial class BloodredVimSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodredVimComponent, BloodredVimBoostActionEvent>(OnBoost);
        SubscribeLocalEvent<BloodredVimComponent, MechEntryEvent>(OnMechEntry);
        SubscribeLocalEvent<BloodredVimComponent, MechExitEvent>(OnMechExit);
    }

    private void OnMechEntry(Entity<BloodredVimComponent> ent, ref MechEntryEvent args)
    {
        var (uid, comp) = ent;
        _actions.AddAction(args.Args.User, ref comp.BoostAction, comp.ActionProto, uid);
    }

    private void OnMechExit(Entity<BloodredVimComponent> ent, ref MechExitEvent args)
    {
        var (uid, comp) = ent;
        if (comp.BoostAction != null)
            _actions.RemoveAction(args.Args.User, comp.BoostAction);
        comp.BoostAction = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var doSim = _net.IsServer || (_net.IsClient && _timing.IsFirstTimePredicted);
        if (!doSim)
            return;

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<BloodredVimComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var physics))
        {
            var boosting = now >= comp.BoostStart && now < comp.BoostEnd;

            if (_net.IsServer && comp.LockHitAndRunComponent && TryComp<VehicleHitAndRunComponent>(uid, out var har))
            {
                var shouldEnable = boosting;
                if (har.CanRunOver != shouldEnable)
                {
                    har.CanRunOver = shouldEnable;
                    Dirty(uid, har);
                }
            }

            if (boosting)
            {
                var total = (float) (comp.BoostEnd - comp.BoostStart).TotalSeconds;
                if (total > 0f)
                {
                    var elapsed = (float) (now - comp.BoostStart).TotalSeconds;
                    var t = Math.Clamp(elapsed / total, 0f, 1f);
                    var throttle = t;
                    var dv = comp.BoostDir * (comp.ThrustAcceleration * throttle) * frameTime;
                    _physics.SetLinearVelocity(uid, physics.LinearVelocity + dv);
                }
            }
        }
    }

    public virtual void OnBoost(Entity<BloodredVimComponent> ent, ref BloodredVimBoostActionEvent args)
    {
        if (args.Handled)
            return;

        var (uid, comp) = ent;

        var from = _xform.GetMapCoordinates(uid);
        var to = _xform.ToMapCoordinates(args.Target);
        if (from.MapId != to.MapId)
            return;

        var aim = to.Position - from.Position;
        var len = aim.Length();
        if (len <= 0.001f)
            return;

        var now = _timing.CurTime;
        comp.BoostStart = now;
        comp.BoostEnd = now + TimeSpan.FromSeconds(comp.BoostDuration);

        comp.BoostDir = aim / len;
        comp.LastPilot = args.Performer;
        comp.EmitElapsed = TimeSpan.Zero;

        args.Handled = true;
    }
}

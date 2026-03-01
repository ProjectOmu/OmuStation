using Content.Goobstation.Server.BSOLifeline.Components;
using Content.Shared.Trigger;
using Content.Shared.Warps;

namespace Content.Goobstation.Server.BSOLifeline.Systems;

using Content.Server.Explosion.EntitySystems;
using Content.Server.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Standing;
using Robust.Server.GameObjects;
using Content.Shared.Rejuvenate;
using Content.Goobstation.Common.CCVar;
using Robust.Shared.Configuration;

public sealed class GoobLifelineSystem : EntitySystem
{
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarpParentOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(EntityUid uid, WarpParentOnTriggerComponent component, TriggerEvent args)
    {
        WarpParent(uid, component);
        args.Handled = true;
    }
    private void WarpParent(EntityUid uid, WarpParentOnTriggerComponent component)
    {

        var dropHandItemsEvent = new DropHandItemsEvent();
        RaiseLocalEvent(uid, ref dropHandItemsEvent);

        var location = FindWarpPoint(component.WarpLocation);

        if (location == null || !TryComp<TransformComponent>(uid, out var transform))
            return;

        var parentUid = transform.ParentUid;
        if (parentUid == EntityUid.Invalid || !HasComp<MobStateComponent>(parentUid))
            return;

        // Reset mind - can be considered if greentext is a concern
        if (_configurationManager.GetCVar(GoobCVars.LifeLineResetMind))
        {
            if (_mindSystem.TryGetMind(parentUid, out var mindId, out var mind))
            {
                var userId = mind.UserId;
                var name = mind.CharacterName;
                _mindSystem.WipeMind(parentUid);
                var newMindId = _mindSystem.CreateMind(userId, name).Owner;
                _mindSystem.TransferTo(newMindId, parentUid, true);
            }
        }
        var coords = _transform.GetMapCoordinates(location.Value);
        _transform.SetMapCoordinates(parentUid, coords);

        if (_configurationManager.GetCVar(GoobCVars.LifeLineRejuvenate))
        {
            RaiseLocalEvent(parentUid, new RejuvenateEvent());
        }

        QueueDel(uid);

    }
    private EntityUid? FindWarpPoint(string location)
    {
        var query = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();

        while (query.MoveNext(out var entity, out var warp, out var transform))
        {
            if (warp.Location == location)
                return entity;
        }

        return null;
    }



}

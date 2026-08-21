// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Shuttles.Components;
using Content.Shared.Objectives.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;

namespace Content.Server.Shuttles.Systems;

/// <summary>
///     Deletes anything with <see cref="SpaceGarbageComponent"/> that has a cross-grid collision with a static body.
/// </summary>
public sealed class SpaceGarbageSystem : EntitySystem
{
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<SpaceGarbageComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(EntityUid uid, SpaceGarbageComponent component, ref StartCollideEvent args)
    {
        if (args.OtherBody.BodyType != BodyType.Static)
            return;

        // omu todo this is bad and hardcoded, fix parenting on corgi meat later.
        // Omu Start -- Steal targets like Prime-cut Corgi meat will no longer delete
        if (HasComp<StealTargetComponent>(uid))
            return;
        // Omu End

        var ourXform = _xformQuery.GetComponent(uid);
        var otherXform = _xformQuery.GetComponent(args.OtherEntity);

        if (ourXform.GridUid == otherXform.GridUid)
            return;

        QueueDel(uid);
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
using Content.Shared.Eye;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Content.Shared.Revenant.Components;
using Content.Shared.Revenant.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;

namespace Content.Server.Revenant.EntitySystems;

public sealed class CorporealSystem : SharedCorporealSystem
{
    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;

    public override void OnStartup(EntityUid uid, CorporealComponent component, ComponentStartup args)
    {
        base.OnStartup(uid, component, args);

        if (TryComp<VisibilityComponent>(uid, out var visibility))
        {
            _visibilitySystem.RemoveLayer((uid, visibility), (int) VisibilityFlags.Ghost, false);
            _visibilitySystem.AddLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
            _visibilitySystem.RefreshVisibility(uid, visibility);
            _physicsSystem.SetCollisionLayer(uid, "fix1", Comp<FixturesComponent>(uid).Fixtures["fix1"], (int) CollisionGroup.Opaque, Comp<FixturesComponent>(uid)); // Omu edit. bullets fly though wraith
        }
    }

    public override void OnShutdown(EntityUid uid, CorporealComponent component, ComponentShutdown args)
    {
        base.OnShutdown(uid, component, args);

        if (TryComp<VisibilityComponent>(uid, out var visibility) && _ticker.RunLevel != GameRunLevel.PostRound)
        {
            _visibilitySystem.AddLayer((uid, visibility), (int) VisibilityFlags.Ghost, false);
            _visibilitySystem.RemoveLayer((uid, visibility), (int) VisibilityFlags.Normal, false);
            _visibilitySystem.RefreshVisibility(uid, visibility);
            _physicsSystem.SetCollisionLayer(uid, "fix1", Comp<FixturesComponent>(uid).Fixtures["fix1"], (int) CollisionGroup.GhostImpassable, Comp<FixturesComponent>(uid)); // Omu edit. bullets fly though wraith
        }
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Omu.Server.Arcade;

public sealed class PolybiusPullSystem : EntitySystem
{
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();
    private readonly HashSet<EntityUid> _caught = new();
    private readonly List<Entity<PolybiusPulledComponent>> _released = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _caught.Clear();

        using var query = EntityQueryEnumerator<PolybiusPullComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pull, out var xform))
        {
            if (xform.Anchored && _power.IsPowered(uid))
                PullInFront((uid, pull, xform), frameTime);
        }

        _released.Clear();
        using var pulled = EntityQueryEnumerator<PolybiusPulledComponent>();
        while (pulled.MoveNext(out var uid, out var comp))
        {
            if (!_caught.Contains(uid))
                _released.Add((uid, comp));
        }

        foreach (var ent in _released)
        {
            Release(ent);
        }
    }

    private void PullInFront(Entity<PolybiusPullComponent, TransformComponent> cabinet, float frameTime)
    {
        var xform = cabinet.Comp2;
        var front = xform.LocalRotation.GetCardinalDir().ToIntVec();
        var cabinetPos = _transform.GetWorldPosition(xform);
        var screenCoords = xform.Coordinates.Offset(new Vector2(front.X, front.Y) * 0.5f);
        var screenPos = _transform.ToMapCoordinates(screenCoords).Position;
        var faceDir = screenPos - cabinetPos;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, cabinet.Comp1.Range, _nearby);

        foreach (var mob in _nearby.Where(mob => !_mobState.IsIncapacitated(mob, mob.Comp)))
        {
            var mobXform = Transform(mob);
            var fromCabinet = _transform.GetWorldPosition(mobXform) - cabinetPos;
            if (fromCabinet.Length() > cabinet.Comp1.Range || Vector2.Dot(fromCabinet, faceDir) < 0.25f)
                continue;

            _caught.Add(mob);
            HoldFacing(mob, cabinetPos);

            if (TryComp<MovedByPressureComponent>(mob, out var pressure) && !pressure.Enabled)
                continue;

            if (!TryComp<PhysicsComponent>(mob, out var physics))
                continue;

            var toward = screenPos - _transform.GetWorldPosition(mobXform);
            if (toward.LengthSquared() < 0.01f)
                continue;

            var impulse = Vector2.Normalize(toward) * cabinet.Comp1.Acceleration * physics.Mass * frameTime;
            _physics.ApplyLinearImpulse(mob, impulse, body: physics);
        }
    }

    private void HoldFacing(EntityUid mob, Vector2 cabinetPos)
    {
        var pulled = EnsureComp<PolybiusPulledComponent>(mob);
        if (!HasComp<NoRotateOnMoveComponent>(mob))
        {
            AddComp<NoRotateOnMoveComponent>(mob);
            pulled.AddedNoRotate = true;
        }

        _rotateToFace.TryFaceCoordinates(mob, cabinetPos);
    }

    private void Release(Entity<PolybiusPulledComponent> ent)
    {
        if (ent.Comp.AddedNoRotate && !_combatMode.IsInCombatMode(ent))
            RemComp<NoRotateOnMoveComponent>(ent);

        RemComp(ent, ent.Comp);
    }
}

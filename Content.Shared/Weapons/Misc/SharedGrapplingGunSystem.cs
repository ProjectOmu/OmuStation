// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Misc;

public abstract class SharedGrapplingGunSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public const string GrapplingJoint = "grappling";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrapplingProjectileComponent, ProjectileEmbedEvent>(OnGrappleCollide);
        SubscribeLocalEvent<GrapplingProjectileComponent, JointRemovedEvent>(OnGrappleJointRemoved);
        SubscribeLocalEvent<CanWeightlessMoveEvent>(OnWeightlessMove);
        SubscribeAllEvent<RequestGrapplingReelMessage>(OnGrapplingReel);

        // TODO: After step trigger refactor, dropping a grappling gun should manually try and activate step triggers it's suppressing.
        SubscribeLocalEvent<GrapplingGunComponent, GunShotEvent>(OnGrapplingShot);
        SubscribeLocalEvent<GrapplingGunComponent, ActivateInWorldEvent>(OnGunActivate);
        SubscribeLocalEvent<GrapplingGunComponent, HandDeselectedEvent>(OnGrapplingDeselected);
    }

    private void OnGrappleJointRemoved(EntityUid uid, GrapplingProjectileComponent component, JointRemovedEvent args)
    {
        if (_netManager.IsServer)
            QueueDel(uid);
    }

    private void OnGrapplingShot(EntityUid uid, GrapplingGunComponent component, ref GunShotEvent args)
    {
        foreach (var (shotUid, _) in args.Ammo)
        {
            if (!HasComp<GrapplingProjectileComponent>(shotUid))
                continue;

            //todo: this doesn't actually support multigrapple
            // At least show the visuals.
            component.Projectile = shotUid.Value;
            Dirty(uid, component);
            var visuals = EnsureComp<JointVisualsComponent>(shotUid.Value);
            visuals.Sprite = component.RopeSprite;
            visuals.Target = uid;
            Dirty(shotUid.Value, visuals);
        }

        TryComp<AppearanceComponent>(uid, out var appearance);
        _appearance.SetData(uid, SharedTetherGunSystem.TetherVisualsStatus.Key, false, appearance);
        Dirty(uid, component);
    }

    private void OnGrapplingDeselected(EntityUid uid, GrapplingGunComponent component, HandDeselectedEvent args)
    {
        SetReeling(uid, component, false, args.User);
    }

    private void OnGrapplingReel(RequestGrapplingReelMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!_hands.TryGetActiveItem(player, out var activeItem) ||
            !TryComp<GrapplingGunComponent>(activeItem, out var grappling))
        {
            return;
        }

        if (msg.Reeling &&
            (!TryComp<CombatModeComponent>(player, out var combatMode) ||
             !combatMode.IsInCombatMode))
        {
            return;
        }

        SetReeling(activeItem.Value, grappling, msg.Reeling, player);
    }

    private void OnWeightlessMove(ref CanWeightlessMoveEvent ev)
    {
        if (ev.CanMove || !TryComp<JointRelayTargetComponent>(ev.Uid, out var relayComp))
            return;

        foreach (var relay in relayComp.Relayed)
        {
            if (TryComp<JointComponent>(relay, out var jointRelay) && jointRelay.GetJoints.ContainsKey(GrapplingJoint))
            {
                ev.CanMove = true;
                return;
            }
        }
    }

    private void OnGunActivate(EntityUid uid, GrapplingGunComponent component, ActivateInWorldEvent args)
    {
        if (!Timing.IsFirstTimePredicted || args.Handled || !args.Complex || component.Projectile is not { } projectile)
            return;

        _audio.PlayPredicted(component.CycleSound, uid, args.User);
        _appearance.SetData(uid, SharedTetherGunSystem.TetherVisualsStatus.Key, true);

        if (_netManager.IsServer)
            QueueDel(projectile);

        component.Projectile = null;
        SetReeling(uid, component, false, args.User);
        _gun.ChangeBasicEntityAmmoCount(uid, 1);

        args.Handled = true;
    }

    private void SetReeling(EntityUid uid, GrapplingGunComponent component, bool value, EntityUid? user)
    {
        if (component.Reeling == value)
            return;

        if (value)
        {
            if (Timing.IsFirstTimePredicted)
                component.Stream = _audio.PlayPredicted(component.ReelSound, uid, user)?.Entity;
        }
        else
        {
            if (Timing.IsFirstTimePredicted)
            {
                component.Stream = _audio.Stop(component.Stream);
            }
        }

        component.Reeling = value;
        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GrapplingGunComponent>();

        while (query.MoveNext(out var uid, out var grappling))
        {
            // Omu start - initial reel/collision check
            bool allowReel = true;
            JointComponent? jointComp = null;
            if (!grappling.Reeling || !TryComp<JointComponent>(uid, out jointComp)) // Omu end
            {
                if (Timing.IsFirstTimePredicted)
                {
                    // Just in case.
                    grappling.Stream = _audio.Stop(grappling.Stream);
                }

                // Omu start - wake up player physics
                if (jointComp?.Relay != null && !grappling.Reeling)
                    _physics.WakeBody(jointComp.Relay.Value);
                // Omu end
                continue;
            }
            // Omu start
            SetReeling(uid, grappling, false, null);

            if (jointComp?.Relay != null)
            {
                var _contactQuery = _physics.GetContacts(jointComp.Relay.Value);
                while (_contactQuery.MoveNext(out var contact))
                {
                    if (contact.Hard)
                    {
                        allowReel = false;
                        break;
                    }
                }
                _physics.WakeBody(jointComp.Relay.Value);
            }

            if (jointComp == null)
                continue;

            if (!jointComp.GetJoints.TryGetValue(GrapplingJoint, out var joint) ||
            joint is not DistanceJoint distance || !allowReel)
            {
                continue;
            }
            // Omu end

            // TODO: This should be on engine.
            distance.MaxLength = MathF.Max(distance.MinLength, distance.MaxLength - grappling.ReelRate * frameTime);
            distance.Length = MathF.Min(distance.MaxLength, distance.Length);

            _physics.WakeBody(joint.BodyAUid);
            _physics.WakeBody(joint.BodyBUid);

            Dirty(uid, jointComp);

            if (distance.MaxLength.Equals(distance.MinLength))
            {
                allowReel = false; // Omu
            }

            // Omu start - final reeling set
            if (allowReel)
                SetReeling(uid, grappling, true, null);
            // Omu end
        }
    }

    /// <summary>
    /// Checks whether the entity is hooked to something via grappling gun.
    /// </summary>
    /// <param name="entity">Entity to check.</param>
    /// <returns>True if hooked, false otherwise.</returns>
    public bool IsEntityHooked(Entity<JointRelayTargetComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        foreach (var uid in entity.Comp.Relayed)
        {
            if (HasComp<GrapplingGunComponent>(uid))
                return true;
        }

        return false;
    }

    private void OnGrappleCollide(EntityUid uid, GrapplingProjectileComponent component, ref ProjectileEmbedEvent args)
    {
        if (!Timing.IsFirstTimePredicted || !args.Weapon.HasValue)
            return;

        var jointComp = EnsureComp<JointComponent>(uid);
        var joint = _joints.CreateDistanceJoint(uid, args.Weapon.Value, id: GrapplingJoint);
        joint.MaxLength = joint.Length + 0.2f;
        joint.Stiffness = 1f;
        joint.MinLength = 1f; // Length of a tile to prevent pulling yourself into / through walls
        // Setting velocity directly for mob movement fucks this so need to make them aware of it.
        // joint.Breakpoint = 4000f;
        Dirty(uid, jointComp);
    }

    [Serializable, NetSerializable]
    protected sealed class RequestGrapplingReelMessage : EntityEventArgs
    {
        public bool Reeling;

        public RequestGrapplingReelMessage(bool reeling)
        {
            Reeling = reeling;
        }
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Projectiles;
using Content.Goobstation.Common.Weapons.Penetration;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Administration.Logs;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Trauma.Projectiles;

/// <summary>
/// Handles predicting projectile hits.
/// This was previously only done serverside.
/// </summary>
public sealed class PredictedProjectileSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedDestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedProjectileSystem _projectile = default!;

    private EntityQuery<ProjectileComponent> _query;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<PredictedShooterComponent> _shooterQuery;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<ProjectileComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _shooterQuery = GetEntityQuery<PredictedShooterComponent>();

        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<ProjectileComponent> ent, ref StartCollideEvent args)
    {
        // This is so entities that shouldn't get a collision are ignored.
        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture || !args.OtherFixture.Hard)
            return;

        DoHit((ent, ent.Comp, args.OurBody), args.OtherEntity, args.OtherFixture);
    }

    /// <summary>
    /// Process a hit for a projectile and a target entity.
    /// This overload uses the first hard fixture on the target,
    /// there should only be 1 hard fixture on a given entity.
    /// Checking multiple hard fixtures would need a collision layer to check against, CBF.
    /// </summary>
    public void DoHit(EntityUid uid, EntityUid target)
    {
        if (!_query.TryComp(uid, out var comp) ||
            !_physicsQuery.TryComp(uid, out var physics) ||
            FindHardFixture(target) is not {} otherFixture)
            return;

        DoHit((uid, comp, physics), target, otherFixture);
    }

    private Fixture? FindHardFixture(EntityUid uid)
    {
        if (!_fixturesQuery.TryComp(uid, out var comp))
            return null;

        foreach (var fixture in comp.Fixtures.Values)
        {
            if (fixture.Hard)
                return fixture;
        }

        return null;
    }

    /// <summary>
    /// Process a hit for a projectile and a target entity.
    /// </summary>
    public void DoHit(Entity<ProjectileComponent, PhysicsComponent> ent, EntityUid target, Fixture otherFixture)
    {
        var (uid, component, ourBody) = ent;
        if (component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        // ignore spent in prediction ticks to allow for embedding to be predicted properly
        if (component.ProjectileSpent && _timing.IsFirstTimePredicted)
            return;

        // lag compensated hits don't go through PreventCollideEvent
        if (component.IgnoredEntities.Contains(target))
            return;

        // it's here so this check is only done once before possible hit
        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            _projectile.SetShooter(uid, component, target);
            _gun.SetTarget(uid, null, out _); // Goobstation
            component.IgnoredEntities.Clear(); // Goobstation
            Dirty(uid, component);
            return;
        }

        var ev = new ProjectileHitEvent(component.Damage * _damageable.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);

        var otherName = ToPrettyString(target);
        var damageRequired = _destructible.GetDestroyedAt(target);
        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            damageRequired -= damageableComponent.TotalDamage;
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }

        // Goob edit start
        TargetBodyPart? targetPart = null;
        if (TryComp(uid, out ProjectileMissTargetPartChanceComponent? missComp) &&
            !missComp.PerfectHitEntities.Contains(target))
            targetPart = TargetBodyPart.Chest;
        var modifiedDamage = _damageable.TryChangeDamage(target,
            ev.Damage,
            component.IgnoreResistances,
            damageable: damageableComponent,
            origin: component.Shooter,
            targetPart: targetPart);
        // Goob edit end
        var deleted = Deleted(target);

        if (modifiedDamage is not null && Exists(component.Shooter))
        {
            // intentionally not predicting so you know if color flashes its 100% a hit
            if (modifiedDamage.AnyPositive() && !deleted && _net.IsServer)
            {
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
            }

            _adminLogger.Add(LogType.BulletHit,
                LogImpact.Medium,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {otherName:target} and dealt {modifiedDamage.GetTotal():damage} damage");

            component.ProjectileSpent = !TryPenetrate((uid, component), modifiedDamage, damageRequired,
                target); // Goob, also modifieddamage

            // Omu start - required for hardlight bow to work properly
            if (component.Penetrate)
            {
                component.ProjectileSpent = false;
            }
            // Omu end
        }
        else
        {
            component.ProjectileSpent = true;
        }

        // Trauma - don't hit the same thing twice if it keeps going, e.g. from lag compensation then actually colliding
        if (!component.ProjectileSpent && !component.IgnoredEntities.Contains(target))
        {
            component.IgnoredEntities.Add(target);
            Dirty(uid, component);
        }

        // Omu - whoever predicted shooting it already played the sound and effects
        EntityUid? predictingUser = _shooterQuery.CompOrNull(uid)?.Shooter;

        if (!deleted)
        {
            _gun.PlayImpactSound(target, modifiedDamage, component.SoundHit, component.ForceSound, predictingUser);

            // Omu - only server kicks the target, clients would kick again on every reprediction
            if (!ourBody.LinearVelocity.IsLengthZero() && _net.IsServer)
                _recoil.KickCamera(target, ourBody.LinearVelocity.Normalized());
        }

        if ((component.DeleteOnCollide && component.ProjectileSpent) || (component.NoPenetrateMask & otherFixture.CollisionLayer) != 0) // Goobstation - Make x-ray arrows not penetrate blob
            PredictedQueueDel(uid);

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform))
        {
            var effectEv = new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates));
            // Omu - server still has to send it to everyone that didn't predict the shot
            if (_net.IsServer)
            {
                var filter = Filter.Pvs(xform.Coordinates, entityMan: EntityManager);
                if (predictingUser != null)
                    filter.RemovePlayerByAttachedEntity(predictingUser.Value);
                RaiseNetworkEvent(effectEv, filter);
            }
            else if (_timing.IsFirstTimePredicted)
            {
                RaiseLocalEvent(effectEv);
            }
        }
    }

    private bool TryPenetrate(Entity<ProjectileComponent> projectile, DamageSpecifier damage, FixedPoint2 damageRequired,
        EntityUid? target = null) //goob pass target
    {
        // If penetration is to be considered, we need to do some checks to see if the projectile should stop.
        if (projectile.Comp.PenetrationThreshold == 0)
            return false;

        // If a damage type is required, stop the bullet if the hit entity doesn't have that type.
        if (projectile.Comp.PenetrationDamageTypeRequirement != null)
        {
            foreach (var requiredDamageType in projectile.Comp.PenetrationDamageTypeRequirement)
            {
                if (damage.DamageDict.Keys.Contains(requiredDamageType))
                    continue;

                return false;
            }
        }
        // Goobstation - Splits penetration change if target have PenetratableComponent
        if (!TryComp<PenetratableComponent>(target, out var penetratable))
        {
            // If the object won't be destroyed, it "tanks" the penetration hit.
            if (damage.GetTotal() < damageRequired)
            {
                return false;
            }

            if (!projectile.Comp.ProjectileSpent)
            {
                projectile.Comp.PenetrationAmount += damageRequired;
                // The projectile has dealt enough damage to be spent.
                if (projectile.Comp.PenetrationAmount >= projectile.Comp.PenetrationThreshold)
                {
                    return false;
                }
            }
        }
        else
        {
            // Goobstation - Here penetration threshold count as "penetration health".
            // If it's lower than damage than penetation damage entity cause it deletes projectile
            if (projectile.Comp.PenetrationThreshold < penetratable.PenetrateDamage)
            {
                projectile.Comp.ProjectileSpent = true;
                return false;
            }

            projectile.Comp.PenetrationThreshold -= FixedPoint2.New(penetratable.PenetrateDamage);
            projectile.Comp.Damage *= (1 - penetratable.DamagePenaltyModifier);
        }

        return true;
    }
}

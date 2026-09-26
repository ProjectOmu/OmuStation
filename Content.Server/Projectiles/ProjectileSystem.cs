// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Projectiles;
using Content.Goobstation.Common.Weapons.Penetration;
using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged.Prediction; // Omu - gun prediction port
using Robust.Shared.Physics; // Omu - FixturesComponent for ProjectileCollide
using Robust.Shared.Physics.Dynamics; // Omu - Fixture for ProjectileCollide
using Robust.Shared.Physics.Components; // Omu - PhysicsComponent for ProjectileCollide
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ColorFlashEffectSystem _color = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        // This is so entities that shouldn't get a collision are ignored.
        // Omu start - only the guards that need the contact's fixtures stay here, the rest moved into
        // ProjectileCollide so a caller that has no contact (the prediction layer) still gets them.
        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard)
            return;

        // The collided layer is passed explicitly so the physics path keeps using the exact fixture it
        // hit, rather than the reconstruction ProjectileCollide has to fall back on for callers with no
        // contact. That keeps this path bit-for-bit identical to the pre-extraction code.
        ProjectileCollide((uid, component, args.OurBody), args.OtherEntity, collidedFixture: args.OtherFixture);
        // Omu end
    }

    // Omu start - hit logic extracted from OnStartCollide for gun prediction.
    /// <summary>
    ///     Applies a projectile hit against <paramref name="target"/>. Extracted from
    ///     <see cref="OnStartCollide"/> so the prediction layer can adjudicate a hit without a physics contact.
    /// </summary>
    /// <param name="predicted">
    ///     True when the call came from the prediction layer rather than from physics.
    ///     Every damage, log and feedback effect below still runs either way - the server is authoritative
    ///     for all of them and the client-side counterpart produces none of them locally, so there is
    ///     nothing to suppress or double up on. The one thing it changes is the projectile's disposal: a
    ///     predicted hit is adjudicated at a rewound position, so the projectile is kept and tagged rather
    ///     than deleted on the spot. See the QueueDel below.
    /// </param>
    /// <param name="collidedFixture">
    ///     The specific fixture that was hit, when the caller came from a physics contact and therefore
    ///     knows it. Callers without a contact leave this null and the collision layer is reconstructed
    ///     from the target's hard fixtures instead. The fixture rather than its layer is passed so the
    ///     layer can be read at the same point in the method the pre-extraction code read it at.
    /// </param>
    public void ProjectileCollide(Entity<ProjectileComponent, PhysicsComponent> projectile, EntityUid target, bool predicted = false, Fixture? collidedFixture = null)
    {
        var (uid, component, ourBody) = projectile;

        // Moved out of OnStartCollide's guard above - these do not depend on the contact.
        if (component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        // A caller with no physics contact (the prediction layer) cannot know which fixture was hit, so
        // for it the collision layer is reconstructed from the target's hard fixtures - OnStartCollide's
        // guard only ever lets a hard other-fixture through, so hard fixtures are the right set. That
        // has to happen HERE, before damage can destroy the target and take its fixtures with it.
        //
        // The physics path does not use this: it passes the fixture itself and reads the layer down at
        // the QueueDel below, exactly where the pre-extraction code read it. That keeps the physics path
        // identical even if something in the damage resolution mutates a collision layer.
        //
        // Both branches are skipped entirely unless the projectile sets a NoPenetrateMask, so every
        // projectile that does not take exactly the same path as before. The sole content user is the
        // x-ray arrow against blob tiles.
        var reconstructedNoPenetrate = false;
        if (component.NoPenetrateMask != 0 && collidedFixture == null && TryComp<FixturesComponent>(target, out var targetFixtures))
        {
            foreach (var fixture in targetFixtures.Fixtures.Values)
            {
                if (!fixture.Hard || (component.NoPenetrateMask & fixture.CollisionLayer) == 0)
                    continue;

                reconstructedNoPenetrate = true;
                break;
            }
        }

        // it's here so this check is only done once before possible hit
        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(uid, component, target);
            _guns.SetTarget(uid, null, out _); // Goobstation
            component.IgnoredEntities.Clear(); // Goobstation
            return;
        }

        var ev = new ProjectileHitEvent(component.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);

        var otherName = ToPrettyString(target);
        var damageRequired = _destructibleSystem.DestroyedAt(target);
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
        var modifiedDamage = _damageableSystem.TryChangeDamage(target,
            ev.Damage,
            component.IgnoreResistances,
            damageable: damageableComponent,
            origin: component.Shooter,
            targetPart: targetPart);
        // Goob edit end
        var deleted = Deleted(target);

        if (modifiedDamage is not null && Exists(component.Shooter))
        {
            if (modifiedDamage.AnyPositive() && !deleted)
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
        if (!deleted)
        {
            _guns.PlayImpactSound(target, modifiedDamage, component.SoundHit, component.ForceSound);

            if (!ourBody.LinearVelocity.IsLengthZero()) // Omu - was args.OurBody, which is projectile.Comp2
                _sharedCameraRecoil.KickCamera(target, ourBody.LinearVelocity.Normalized()); // Omu - as above
        }

        // Omu - the physics path reads the hit fixture's layer right here, as it did before the
        // extraction; only a caller with no contact falls back to the reconstruction computed above.
        var noPenetrate = component.NoPenetrateMask != 0 &&
                          (collidedFixture is { } hitFixture
                              ? (component.NoPenetrateMask & hitFixture.CollisionLayer) != 0
                              : reconstructedNoPenetrate);

        if ((component.DeleteOnCollide && component.ProjectileSpent) || noPenetrate) // Goobstation - Make x-ray arrows not penetrate blob
        {
            // Omu start - gun prediction port (Phase 2). A predicted hit is adjudicated against a
            // REWOUND position, which is behind where the shooter's client has already drawn the
            // bullet. Deleting it now makes the bullet vanish in mid-air, short of the target it just
            // hit. So instead keep it and record how far it still has to travel; the prediction
            // system's Update deletes it once it arrives, and the client hides its sprite there too.
            // Unreachable unless omu.gun_prediction is on, because only an accepted predicted hit
            // passes predicted: true.
            if (predicted)
            {
                var predictedHit = EnsureComp<PredictedProjectileHitComponent>(uid);
                predictedHit.Origin = _xform.GetMoverCoordinates(uid);

                if (predictedHit.Origin.TryDistance(EntityManager, _xform, _xform.GetMoverCoordinates(target), out var hitDistance))
                    predictedHit.Distance = hitDistance;

                Dirty(uid, predictedHit);
            }
            else
            {
                QueueDel(uid);
            }
            // Omu end
        }

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform))
        {
            RaiseNetworkEvent(new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates)), Filter.Pvs(xform.Coordinates, entityMan: EntityManager));
        }
    }
    // Omu end

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

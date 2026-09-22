// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.Projectiles;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Cargo.Systems;
using Content.Server.Weapons.Ranged.Components;
using Content.Shared.Cargo;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Containers;
using Content.Shared._Lavaland.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Content.Goobstation.Common.Weapons.Ranged;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Effects;
using Content.Shared.PowerCell;
using Robust.Shared.Random;
// Omu start - gun prediction port (Phase 2).
using Content.Omu.Common.CCVar;
using Content.Server._RMC14.Movement;
using Robust.Shared.Configuration;
// Omu end

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem : SharedGunSystem
{
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    // Goobstation
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    // Omu start - gun prediction port (Phase 2).
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly RMCLagCompensationSystem _rmcLagCompensation = default!;

    /// <summary>
    ///     Mirror of <see cref="OmuCVars.GunPrediction"/>.
    /// </summary>
    /// <remarks>
    ///     Read here rather than from <c>SharedGunPredictionSystem.GunPrediction</c> because that
    ///     class is abstract and this file must not depend on a concrete subclass of it existing.
    ///     Both copies are fed by the same replicated CVar, so they cannot disagree.
    /// </remarks>
    private bool _gunPrediction;
    // Omu end

    private const float DamagePitchVariation = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, PriceCalculationEvent>(OnBallisticPrice);

        // Omu - gun prediction port (Phase 2).
        Subs.CVar(_cfg, OmuCVars.GunPrediction, v => _gunPrediction = v, true);
    }

    // Omu start - gun prediction port (Phase 2).
    /// <summary>
    ///     Applies the tick the shooter says it last had authoritative state for, before the shot is
    ///     adjudicated, so lag compensation rewinds targets to what the shooter was actually seeing.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <b>The CVar guard is load-bearing, and not for the reason it looks like.</b> The client fills
    ///     <see cref="RequestShootEvent.LastRealTick"/> <i>unconditionally</i> - it is not left at
    ///     <c>default</c> when prediction is off - so this is not a "reject the empty value" guard.
    ///     </para>
    ///     <para>
    ///     It is load-bearing because the per-session store this writes to is shared with everything
    ///     else that uses lag compensation, including the Phase 1 melee and hitscan paths, which are
    ///     already shipped and are fed by <c>RMCLagCompensationSystem</c>'s own heartbeat. Writing here
    ///     while prediction is off would give the shoot request a second, unrequested influence over
    ///     those paths' rewind depth - a behaviour change on a feature that is supposed to be inert,
    ///     and one a client could drive on purpose. Turning the feature off must mean this store is
    ///     fed only by the heartbeat, exactly as it was before this port.
    ///     </para>
    ///     <para>
    ///     No clamping happens here on purpose: <c>SetLastRealTick</c> is the single write point and
    ///     already bounds the value both ways (never ahead of the server, never older than the
    ///     position history). A second clamp here could only ever disagree with that one.
    ///     </para>
    ///     <para>
    ///     Also unlike the heartbeat, no tick is subtracted. The heartbeat's
    ///     <c>RMCSetLastRealTickEvent</c> handler steps back one tick because it reports a rendered,
    ///     interpolated frame; a shoot request names the tick the shot itself was predicted on, and
    ///     the source treats it verbatim.
    ///     </para>
    ///     <para>
    ///     The substep IS passed through, unlike the source, which called the tick-only overload and
    ///     so reset the substep to zero on every shot - quietly coarsening a store shared with the
    ///     melee and hitscan rewind. With guns now adjudicated by Path B, which is substep-aware,
    ///     dropping it would also have coarsened gun adjudication itself.
    ///     </para>
    /// </remarks>
    protected override void OnShootRequestReceived(RequestShootEvent msg, ICommonSession session)
    {
        if (!_gunPrediction)
            return;

        _rmcLagCompensation.SetLastRealTick(session.UserId, msg.LastRealTick, msg.LastRealSubstep);
    }
    // Omu end

    private void OnBallisticPrice(Entity<BallisticAmmoProviderComponent> ent, ref PriceCalculationEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.Proto) || ent.Comp.UnspawnedCount == 0)
            return;

        if (!ProtoManager.TryIndex<EntityPrototype>(ent.Comp.Proto, out var proto))
        {
            Log.Error($"Unable to find fill prototype for price on {ent.Comp.Proto} on {ToPrettyString(ent)}");
            return;
        }

        // Probably good enough for most.
        var price = _pricing.GetEstimatedPrice(proto);
        args.Price += price * ent.Comp.UnspawnedCount;
    }

    public override void Shoot(Entity<GunComponent> gun, List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates, EntityCoordinates toCoordinates, out bool userImpulse, EntityUid? user = null, bool throwItems = false,
        // Omu start - gun prediction port (Phase 2). predictedProjectiles/userSession are unused on
        // this side: the pairing is done afterwards, off GunProjectilesShotEvent, which
        // SharedGunSystem raises with exactly these two values plus spawnedProjectiles.
        List<int>? predictedProjectiles = null,
        ICommonSession? userSession = null,
        List<EntityUid>? spawnedProjectiles = null)
        // Omu end
    {
        userImpulse = true;

        if (user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, gun, ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
            {
                userImpulse = false;
                return;
            }
        }

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        var mapAngle = mapDirection.ToAngle();
        var angle = GetRecoilAngle(Timing.CurTime, gun, mapDirection.ToAngle(), user);  // Goobstation user

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromEnt = MapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(_map.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        var toMapBeforeRecoil = toMap; // Goobstation

        // Update shot based on the recoil
        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);

        // I must be high because this was getting tripped even when true.
        // DebugTools.Assert(direction != Vector2.Zero);
        var shotProjectiles = new List<EntityUid>(ammo.Count);

        foreach (var (ent, shootable) in ammo)
        {
            // pneumatic cannon doesn't shoot bullets it just throws them, ignore ammo handling
            if (throwItems && ent != null)
            {
                ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, user,
                targetCoordinates: toMapBeforeRecoil, // Goobstation
                spawnedProjectiles: spawnedProjectiles); // Omu - gun prediction port (Phase 2)
                shotProjectiles.Add(ent.Value); // Goobstation
                continue;
            }

            // TODO: Clean this up in a gun refactor at some point - too much copy pasting
            switch (shootable)
            {
                // Cartridge shoots something else
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        var uid = Spawn(cartridge.Prototype, fromEnt);
                        CreateAndFireProjectiles(uid, cartridge);

                        RaiseLocalEvent(ent!.Value, new AmmoShotEvent()
                        {
                            FiredProjectiles = shotProjectiles
                        });

                        SetCartridgeSpent(ent.Value, cartridge, true);

                        if (cartridge.DeleteOnSpawn)
                            Del(ent.Value);
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                    }

                    // Something like ballistic might want to leave it in the container still
                    if (!cartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value))
                        EjectCartridge(ent.Value, angle);

                    Dirty(ent!.Value, cartridge);
                    break;
                // Ammo shoots itself
                case AmmoComponent newAmmo:
                    if (ent == null)
                        break;
                    CreateAndFireProjectiles(ent.Value, newAmmo);

                    break;
                case HitscanAmmoComponent:
                    if (ent == null)
                        break;

                    var hitscanEv = new HitscanTraceEvent
                    {
                        FromCoordinates = fromCoordinates,
                        ShotDirection = mapDirection.Normalized(),
                        Gun = gun,
                        Shooter = user,
                        Target = gun.Comp.Target,
                    };
                    RaiseLocalEvent(ent.Value, ref hitscanEv);

                    Del(ent);

                    Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        RaiseLocalEvent(gun, new AmmoShotEvent()
        {
            FiredProjectiles = shotProjectiles,
        });

        // Goobstation start
        if (user.HasValue)
            RaiseLocalEvent(user.Value, new AmmoShotUserEvent()
            {
                Gun = gun,
                FiredProjectiles = shotProjectiles,
            });
        // Goobstation end

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gun, ref spreadEvent);

                var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2, ammoSpreadComp.Count);

                ShootOrThrow(ammoEnt, angles[0].ToVec(), gunVelocity, gun, user,
                    targetCoordinates: toMapBeforeRecoil, // Goobstation
                    spawnedProjectiles: spawnedProjectiles); // Omu - gun prediction port (Phase 2)
                shotProjectiles.Add(ammoEnt);

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    var newuid = Spawn(ammoSpreadComp.Proto, fromEnt);
                    // Lavaland Change: Raise event when a projectile/pellet is fired from a gun.
                    RaiseLocalEvent(gun, new ProjectileShotEvent()
                    {
                        FiredProjectile = newuid
                    });
                    SetProjectilePerfectHitEntities(newuid, user, new MapCoordinates(toMap, fromMap.MapId));
                    // Lavaland end
                    ShootOrThrow(newuid, angles[i].ToVec(), gunVelocity, gun, user,
                    targetCoordinates: toMapBeforeRecoil, // Goob
                    spawnedProjectiles: spawnedProjectiles); // Omu - gun prediction port (Phase 2)
                    shotProjectiles.Add(newuid);
                }
            }
            else
            {
                ShootOrThrow(ammoEnt, mapDirection, gunVelocity, gun, user,
                targetCoordinates: toMapBeforeRecoil, // Goobstation
                spawnedProjectiles: spawnedProjectiles); // Omu - gun prediction port (Phase 2)
                shotProjectiles.Add(ammoEnt);
            }

            MuzzleFlash(gun, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
        }
    }

    // Goobstation start
    public TargetBodyPart? GetTargetPart(Entity<TargetingComponent?>? ent,
        MapCoordinates shootCoords,
        MapCoordinates targetCoords)
    {
        if (shootCoords.MapId != targetCoords.MapId || ent == null)
            return null;

        var targeting = ent.Value;

        if (!Resolve(targeting, ref targeting.Comp, false))
            return null;

        var dist = (shootCoords.Position - targetCoords.Position).Length();
        var missChance = MathHelper.Lerp(0f, 1f, Math.Clamp(dist / 2f, 0f, 1f));
        return Random.Prob(missChance) ? TargetBodyPart.Chest : targeting.Comp.Target;
    }

    private void SetProjectilePerfectHitEntities(EntityUid projectile,
        Entity<TargetingComponent?>? shooter,
        MapCoordinates coords)
    {
        if (shooter == null)
            return;

        var ent = shooter.Value;

        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var comp = EnsureComp<ProjectileMissTargetPartChanceComponent>(projectile);
        var look = _lookup.GetEntitiesInRange<BodyComponent>(coords, 2f, LookupFlags.Dynamic);
        foreach (var (uid, body) in look)
        {
            if (body.BodyType != Shared._Shitmed.Body.BodyType.Complex)
                continue;

            var part = GetTargetPart(shooter, coords, _transform.GetMapCoordinates(ent));

            if (part is null or TargetBodyPart.Chest)
                continue;

            comp.PerfectHitEntities.Add(uid);
        }
    }
    // Goobstation end

    /// <param name="spawnedProjectiles">
    ///     Omu - gun prediction port (Phase 2). Collects every entity that actually leaves the barrel
    ///     as a projectile, so <c>GunProjectilesShotEvent</c> can pair them, in order, with the
    ///     client-side copies the shooter already drew. Null for shots nobody is predicting.
    ///     A thrown item is deliberately not recorded: it takes the <c>TryThrow</c> path below, has no
    ///     <c>ProjectileComponent</c>, and so has no predicted counterpart to pair with - recording it
    ///     would shift every later index by one. An embeddable projectile (arrow, harpoon, syringe
    ///     dart) is not recorded either, for the reason given where it is skipped below; the client
    ///     skips it at the same point, so the indices still line up.
    /// </param>
    private void ShootOrThrow(EntityUid uid, Vector2 mapDirection, Vector2 gunVelocity, Entity<GunComponent> gun, EntityUid? user,
        Vector2? targetCoordinates = null, // Goobstation
        List<EntityUid>? spawnedProjectiles = null) // Omu - gun prediction port (Phase 2)
    {
        if (gun.Comp.Target is { } target && !TerminatingOrDeleted(target))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Target = target;
            Dirty(uid, targeted);
        }

        // Do a throw
        if (!TryComp(uid, out ProjectileComponent? projectileComp)) // Omu
        {
            RemoveShootable(uid);
            // TODO: Someone can probably yeet this a billion miles so need to pre-validate input somewhere up the call stack.
            ThrowingSystem.TryThrow(uid, mapDirection, gun.Comp.ProjectileSpeedModified, user);
            return;
        }
        projectileComp.Damage *= gun.Comp.DamageModifier; // Omu
        ShootProjectile(uid, mapDirection, gunVelocity, gun, user, gun.Comp.ProjectileSpeedModified,
        targetCoordinates); // Goobstation
        // Omu - gun prediction port (Phase 2). Embeddable projectiles (arrows, harpoons, syringe
        // darts) are not recorded, mirroring the client's ShootPredicted: pairing hides the real
        // projectile from its shooter until it is deleted, and one that embeds or lands is never
        // deleted, so it would stay invisible to them. Both sides must skip the same entries or every
        // later id in the shot would pair with the wrong projectile.
        if (!HasComp<EmbeddableProjectileComponent>(uid))
            spawnedProjectiles?.Add(uid);
    }

    /// <summary>
    /// Gets a linear spread of angles between start and end.
    /// </summary>
    /// <param name="start">Start angle in degrees</param>
    /// <param name="end">End angle in degrees</param>
    /// <param name="intervals">How many shots there are</param>
    public Angle[] LinearSpread(Angle start, Angle end, int intervals) // Goob edit
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }

    private Angle GetRecoilAngle(TimeSpan curTime, GunComponent component, Angle direction, EntityUid? user = null) // Goobstation user
    {
        var timeSinceLastFire = (curTime - component.LastFire).TotalSeconds;
        var minTheta = Math.Min(component.MinAngleModified.Theta, component.MaxAngleModified.Theta); // goob edit make min max work properly
        var maxTheta = Math.Max(component.MinAngleModified.Theta, component.MaxAngleModified.Theta); // goob edit reverse recoil direction for funny mechanics
        var newTheta = MathHelper.Clamp(component.CurrentAngle.Theta + component.AngleIncreaseModified.Theta - component.AngleDecayModified.Theta * timeSinceLastFire, minTheta, maxTheta); // goob edit
        component.CurrentAngle = new Angle(newTheta);
        component.LastFire = component.NextFire;

        // Convert it so angle can go either side.
        var random = Random.NextFloat(-0.5f, 0.5f);

        // Goobstation start
        var angleEv = new GetRecoilModifiersEvent()
        {
            Gun = component.Owner,
            User = user ?? component.Owner
        };
        if (user != null)
            RaiseLocalEvent(user.Value, angleEv);
        RaiseLocalEvent(component.Owner, angleEv);
        random *= angleEv.Modifier;
        // Goobstation end

        var spread = component.CurrentAngle.Theta * random;
        var angle = new Angle(direction.Theta + component.CurrentAngle.Theta * random);
        DebugTools.Assert(Math.Abs(spread) <= maxTheta); // goob edit
        return angle;
    }

    protected override void Popup(string message, EntityUid? uid, EntityUid? user) { }

    protected override void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? user = null)
    {
        var filter = Filter.Pvs(gunUid, entityManager: EntityManager);

        if (TryComp<ActorComponent>(user, out var actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(message, filter);
    }

    public override void PlayImpactSound(EntityUid otherEntity, DamageSpecifier? modifiedDamage, SoundSpecifier? weaponSound, bool forceWeaponSound)
    {
        DebugTools.Assert(!Deleted(otherEntity), "Impact sound entity was deleted");

        // Like projectiles and melee,
        // 1. Entity specific sound
        // 2. Ammo's sound
        // 3. Nothing
        var playedSound = false;

        if (!forceWeaponSound && modifiedDamage != null && modifiedDamage.GetTotal() > 0 && TryComp<RangedDamageSoundComponent>(otherEntity, out var rangedSound))
        {
            var type = SharedMeleeWeaponSystem.GetHighestDamageSound(modifiedDamage, ProtoManager);

            if (type != null && rangedSound.SoundTypes?.TryGetValue(type, out var damageSoundType) == true)
            {
                Audio.PlayPvs(damageSoundType, otherEntity, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (type != null && rangedSound.SoundGroups?.TryGetValue(type, out var damageSoundGroup) == true)
            {
                Audio.PlayPvs(damageSoundGroup, otherEntity, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
        }

        if (!playedSound && weaponSound != null)
        {
            Audio.PlayPvs(weaponSound, otherEntity);
        }
    }
}

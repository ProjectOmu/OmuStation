// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Client.Animations;
using Content.Client.Clickable;
using Content.Client.Items;
using Content.Client.Weapons.Ranged.Components;
using Content.Shared._Goobstation.Heretic.Components;
using Content.Shared.Camera;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Animations;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SharedGunSystem = Content.Shared.Weapons.Ranged.Systems.SharedGunSystem;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;
using Content.Shared._Omu.Changeling;
// Omu start - gun prediction port (Phase 2).
using Content.Client._RMC14.Movement;
using Content.Goobstation.Common.Weapons.Multishot;
using Content.Omu.Common.CCVar;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Item;
using Content.Shared.Projectiles;
using Robust.Shared.Player;
// Omu end

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem : SharedGunSystem
{
    [Dependency] private readonly AnimationPlayerSystem _animPlayer = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;
    [Dependency] private readonly ClickableSystem _clickable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

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

    public static readonly EntProtoId HitscanProto = "HitscanEffect";
    private GunTargetEntityComparer _comparer = default!;

    public bool SpreadOverlay
    {
        get => _spreadOverlay;
        set
        {
            if (_spreadOverlay == value)
                return;

            _spreadOverlay = value;

            if (_spreadOverlay)
            {
                _overlayManager.AddOverlay(new GunSpreadOverlay(
                    EntityManager,
                    _eyeManager,
                    Timing,
                    _inputManager,
                    _player,
                    this,
                    TransformSystem));
            }
            else
            {
                _overlayManager.RemoveOverlay<GunSpreadOverlay>();
            }
        }
    }

    private bool _spreadOverlay;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
        SubscribeLocalEvent<AmmoCounterComponent, ItemStatusCollectMessage>(OnAmmoCounterCollect);
        SubscribeAllEvent<MuzzleFlashEvent>(OnMuzzleFlash);

        // Plays animated effects on the client.
        SubscribeNetworkEvent<HitscanEvent>(OnHitscan);

        InitializeMagazineVisuals();
        InitializeSpentAmmo();

        // Omu - gun prediction port (Phase 2).
        Subs.CVar(_cfg, OmuCVars.GunPrediction, v => _gunPrediction = v, true);

        _comparer = new GunTargetEntityComparer();
    }


    private void OnMuzzleFlash(MuzzleFlashEvent args)
    {
        var gunUid = GetEntity(args.Uid);

        CreateEffect(gunUid, args, gunUid);
    }

    private void OnHitscan(HitscanEvent ev)
    {
        // ALL I WANT IS AN ANIMATED EFFECT

        // TODO EFFECTS
        // This is very jank
        // because the effect consists of three unrelatd entities, the hitscan beam can be split appart.
        // E.g., if a grid rotates while part of the beam is parented to the grid, and part of it is parented to the map.
        // Ideally, there should only be one entity, with one sprite that has multiple layers
        // Or at the very least, have the other entities parented to the same entity to make sure they stick together.
        foreach (var a in ev.Sprites)
        {
            if (a.Sprite is not SpriteSpecifier.Rsi rsi)
                continue;

            var coords = GetCoordinates(a.coordinates);

            if (!TryComp(coords.EntityId, out TransformComponent? relativeXform))
                continue;

            var ent = Spawn(HitscanProto, coords);
            var sprite = Comp<SpriteComponent>(ent);

            var xform = Transform(ent);
            var targetWorldRot = a.angle + _xform.GetWorldRotation(relativeXform);
            var delta = targetWorldRot - _xform.GetWorldRotation(xform);
            _xform.SetLocalRotationNoLerp(ent, xform.LocalRotation + delta, xform);

            sprite[EffectLayers.Unshaded].AutoAnimated = false;
            _sprite.LayerSetSprite((ent, sprite), EffectLayers.Unshaded, rsi);
            _sprite.LayerSetRsiState((ent, sprite), EffectLayers.Unshaded, rsi.RsiState);
            _sprite.SetScale((ent, sprite), new Vector2(a.Distance, 1f));
            sprite[EffectLayers.Unshaded].Visible = true;

            var anim = new Animation()
            {
                Length = TimeSpan.FromSeconds(0.48f),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick()
                    {
                        LayerKey = EffectLayers.Unshaded,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(rsi.RsiState, 0f),
                        }
                    }
                }
            };

            _animPlayer.Play(ent, anim, "hitscan-effect");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null || !TryComp<CombatModeComponent>(entityNull, out var combat) || !combat.IsInCombatMode)
        {
            return;
        }

        var entity = entityNull.Value;

        if (TryComp<MechPilotComponent>(entity, out var mechPilot)) // Goobstation
            entity = mechPilot.Mech;

        if (!TryGetGun(entity, out var gun))
        {
            return;
        }

        if (TryComp<EntropicPlumeAffectedComponent>(entity, out var affected) &&
            affected.NextAttack + TimeSpan.FromSeconds(0.1f) > Timing.CurTime) // Goobstation
            return;

        // Omustation Start
        if (TryComp<BerserkAffectedComponent>(entity, out var berserk) &&
            berserk.NextAttack + TimeSpan.FromSeconds(0.1f) > Timing.CurTime)
            return;
        // Omustation End

        var useKey = gun.Comp.UseKey ? EngineKeyFunctions.Use : EngineKeyFunctions.UseSecondary;

        if (_inputSystem.CmdStates.GetState(useKey) != BoundKeyState.Down && !gun.Comp.BurstActivated)
        {
            if (gun.Comp.ShotCounter != 0)
                RaisePredictiveEvent(new RequestStopShootEvent { Gun = GetNetEntity(gun) });
            return;
        }

        if (gun.Comp.NextFire > Timing.CurTime)
            return;

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
        {
            if (gun.Comp.ShotCounter != 0)
                RaisePredictiveEvent(new RequestStopShootEvent { Gun = GetNetEntity(gun) });

            return;
        }

        // Define target coordinates relative to gun entity, so that network latency on moving grids doesn't fuck up the target location.
        var target = GetBestTarget(_eyeManager.CurrentEye, mousePos);

        var coordinates = TransformSystem.ToCoordinates(entity, mousePos);

        Log.Debug($"Sending shoot request tick {Timing.CurTick} / {Timing.CurTime}");

        // Omu start - gun prediction port (Phase 2).
        // The predicted shot has to happen *before* the request is built. RaisePredictiveEvent
        // serialises and sends the message first and only then raises it locally, so any projectile
        // spawned by the local raise (which is what ends up calling Shoot below) would already have
        // missed the boat and Shot would always go out null.
        var shot = _gunPrediction ? PredictShot(entity, gun, coordinates, target) : null;
        // Omu end

        RaisePredictiveEvent(new RequestShootEvent
        {
            Target = target,
            Coordinates = GetNetCoordinates(coordinates),
            Gun = GetNetEntity(gun),
            // Omu start - gun prediction port (Phase 2).
            Shot = shot,
            // The client half of lag compensation exposes the engine's own IGameTiming.LastRealTick
            // through this accessor; going through it keeps the value the server is told here
            // identical to the one the heartbeat reports.
            LastRealTick = _rmcLagCompensation.GetLastRealTick(null),
            LastRealSubstep = _rmcLagCompensation.GetClientSubstep(),
            // Omu end
        });
    }

    // Omu start - gun prediction port (Phase 2).
    /// <summary>
    ///     A predicting client fires the shot itself in <see cref="PredictShot"/>, before the request
    ///     is raised, so the base handler must not fire it a second time when the event comes back
    ///     round locally.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Only the first-time prediction pass is suppressed. Prediction replays still re-run the
    ///     shot through the base handler, which is what reconciles the local copy against the server.
    ///     </para>
    ///     <para>
    ///     With <c>omu.gun_prediction</c> off, <see cref="PredictShot"/> never runs, this returns true
    ///     and the control flow is exactly what it was before the port.
    ///     </para>
    /// </remarks>
    protected override bool ShouldAdjudicateShootRequest()
    {
        return !(_gunPrediction && Timing.IsFirstTimePredicted);
    }

    /// <summary>
    ///     Runs this shot locally, ahead of the request going out, and returns the ids of the
    ///     client-side projectiles it drew.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     This has to run <i>before</i> <c>RaisePredictiveEvent</c>, not after: that method puts the
    ///     message on the wire and only then raises it locally, so anything spawned by the local
    ///     raise is already too late to have its ids travel in the event.
    ///     </para>
    ///     <para>
    ///     The two assignments below are the same ones <c>SharedGunSystem.OnShootRequest</c> makes
    ///     before it adjudicates, Goobstation burst target lock included, so this pass and the
    ///     replayed ones agree on what was aimed at. <paramref name="user"/> is already the mech
    ///     where one is being piloted, because the caller resolved that.
    ///     </para>
    ///     <para>
    ///     The base handler does not fire the shot a second time when the request comes back round
    ///     locally: <see cref="ShouldAdjudicateShootRequest"/> suppresses the first-time-predicted
    ///     pass, so <c>ShotAttemptedEvent</c> is raised once and an empty gun clicks once.
    ///     </para>
    /// </remarks>
    /// <returns>The raw ids of the predicted copies, or null if nothing was drawn.</returns>
    private List<int>? PredictShot(EntityUid user, Entity<GunComponent> gun, EntityCoordinates coordinates, NetEntity? target)
    {
        if (_player.LocalSession is not { } session)
            return null;

        // The two rejections SharedGunSystem.OnShootRequest applies that this method cannot infer
        // from the caller: a multishot gun is driven by a different path entirely, and a carried
        // entity (a held felinid) may not shoot at all. Predicting either would leave copies on
        // screen that the server never pairs with anything, because it never fires.
        if (HasComp<MultishotComponent>(gun.Owner) || HasComp<ItemComponent>(user))
            return null;

        // A gun can opt out of prediction entirely. Honouring it HERE, and not only on the server,
        // is what makes the opt-out worth having: the server refuses to pair such a gun's
        // projectiles, so if the client still drew copies for them nothing would ever retire those
        // copies and the shooter would be left with a ghost bullet beside every real one for the
        // projectile's whole despawn lifetime. Refusing to draw them in the first place is the only
        // version of this marker that reduces desync rather than adding to it.
        if (HasComp<GunIgnorePredictionComponent>(gun.Owner))
            return null;

        gun.Comp.ShootCoordinates = coordinates;

        var potentialTarget = GetEntity(target);
        if (gun.Comp.Target == null || !gun.Comp.BurstActivated || !gun.Comp.LockOnTargetBurst)
            gun.Comp.Target = potentialTarget;

        var projectiles = AttemptShoot(user, gun, null, session);

        if (projectiles == null || projectiles.Count == 0)
            return null;

        var ids = new List<int>(projectiles.Count);
        foreach (var projectile in projectiles)
        {
            ids.Add(projectile.Id);
        }

        return ids;
    }
    // Omu end

    public override void Shoot(Entity<GunComponent> gun, List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates, EntityCoordinates toCoordinates, out bool userImpulse, EntityUid? user = null, bool throwItems = false,
        // Omu start - gun prediction port (Phase 2). predictedProjectiles/userSession are unused on
        // this side - the client is the thing doing the predicting, it does not need telling.
        List<int>? predictedProjectiles = null,
        ICommonSession? userSession = null,
        List<EntityUid>? spawnedProjectiles = null)
        // Omu end
    {
        userImpulse = true;

        // Rather than splitting client / server for every ammo provider it's easier
        // to just delete the spawned entities. This is for programmer sanity despite the wasted perf.
        // This also means any ammo specific stuff can be grabbed as necessary.
        var direction = TransformSystem.ToMapCoordinates(fromCoordinates).Position - TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var worldAngle = direction.ToAngle().Opposite();

        // Omu start - gun prediction port (Phase 2).
        // With the CVar off this is false and every line it guards is skipped, leaving the method
        // byte-for-byte what it was: the client still spawns nothing and still deletes client-side
        // ammo, exactly as the comment above describes.
        //
        // IsFirstTimePredicted is part of the guard rather than an afterthought. Prediction replays
        // re-run this method once per replayed tick until the server acknowledges the shot, and a
        // client-side entity survives a prediction reset - which is the whole reason it can stand in
        // for a projectile - so without it every replay would stack another bullet on screen.
        // GunIgnorePredictionComponent is re-checked here rather than relying on PredictShot's
        // rejection, because Shoot is also reached on prediction replays, which do not go through
        // PredictShot at all.
        var predictProjectiles = _gunPrediction &&
                                 spawnedProjectiles != null &&
                                 Timing.IsFirstTimePredicted &&
                                 !HasComp<GunIgnorePredictionComponent>(gun.Owner);
        // Omu end

        foreach (var (ent, shootable) in ammo)
        {
            if (throwItems)
            {
                Recoil(user, direction, gun.Comp.CameraRecoilScalarModified);
                if (IsClientSide(ent!.Value))
                    Del(ent.Value);
                else
                    RemoveShootable(ent.Value);
                continue;
            }

            // TODO: Clean this up in a gun refactor at some point - too much copy pasting
            switch (shootable)
            {
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        // Omu start - gun prediction port (Phase 2). The server spawns
                        // cartridge.Prototype here; draw a client-side copy of it so the shooter
                        // sees the bullet leave the barrel now instead of a round trip later.
                        if (predictProjectiles)
                            ShootPredicted(gun, user, cartridge.Prototype, fromCoordinates, toCoordinates, spawnedProjectiles!);
                        // Omu end
                        SetCartridgeSpent(ent!.Value, cartridge, true);
                        MuzzleFlash(gun, cartridge, worldAngle, user);
                        Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                        Recoil(user, direction, gun.Comp.CameraRecoilScalarModified);
                        // TODO: Can't predict entity deletions.
                        //if (cartridge.DeleteOnSpawn)
                        //    Del(cartridge.Owner);
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                    }

                    if (IsClientSide(ent!.Value))
                        Del(ent.Value);

                    break;
                case AmmoComponent newAmmo:
                    // Omu start - gun prediction port (Phase 2). Here the ammo entity *is* the
                    // projectile on the server. The client cannot launch the networked one - it is
                    // about to be removed from the gun and the server owns its physics - so it
                    // spawns a client-side copy from the same prototype. An entity assembled in
                    // code has no prototype; ShootPredicted reads that as "nothing to predict".
                    if (predictProjectiles)
                        ShootPredicted(gun, user, MetaData(ent!.Value).EntityPrototype?.ID, fromCoordinates, toCoordinates, spawnedProjectiles!);
                    // Omu end
                    MuzzleFlash(gun, newAmmo, worldAngle, user);
                    Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                    Recoil(user, direction, gun.Comp.CameraRecoilScalarModified);
                    if (IsClientSide(ent!.Value))
                        Del(ent.Value);
                    else
                        RemoveShootable(ent.Value);
                    break;
                case HitscanAmmoComponent:
                    Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                    Recoil(user, direction, gun.Comp.CameraRecoilScalarModified);
                    break;
            }
        }
    }

    // Omu start - gun prediction port (Phase 2).
    /// <summary>
    ///     Draws this client's own, local copy of a projectile the server is about to spawn, and
    ///     records its id so the shoot request can tell the server which copy is which.
    /// </summary>
    /// <param name="proto">
    ///     Prototype of the projectile the server will spawn, or null if it cannot be named - in
    ///     which case nothing is predicted and the shooter simply waits for the real bullet.
    /// </param>
    /// <param name="spawnedProjectiles">Collects the copies, in the order the server spawns them.</param>
    /// <remarks>
    ///     <para>
    ///     The copy is made with plain <c>Spawn</c>, which on the client produces a client-side
    ///     entity. That is deliberate and it is not the same as <c>PredictedSpawn*</c>: a
    ///     <c>PredictedSpawnComponent</c> entity is deleted outright on every prediction reset and
    ///     re-created by the replay, whereas this copy has to outlive the reset and stay on screen
    ///     until the authoritative projectile arrives and the reconciliation retires it. Every other
    ///     client-only entity in this file - the hitscan beam, the muzzle flash - is made the same
    ///     way.
    ///     </para>
    ///     <para>
    ///     The copy is flown with <c>ShootProjectile</c>, the same shared maths the server uses, so
    ///     the two agree on speed (including the Omu <c>ProjectileSpeedModifier</c>) and on the
    ///     Goobstation target coordinates. Two things it cannot match: the server's
    ///     <c>GetRecoilAngle</c> spread, which is drawn from the server's RNG and also mutates gun
    ///     state that is not the client's to touch, so the copy flies down the unspread line; and
    ///     the damage multiplier, which is meaningless on a copy that never adjudicates a hit.
    ///     </para>
    /// </remarks>
    private void ShootPredicted(
        Entity<GunComponent> gun,
        EntityUid? user,
        string? proto,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        List<EntityUid> spawnedProjectiles)
    {
        if (proto == null)
            return;

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;

        if (mapDirection == Vector2.Zero)
            return;

        var mapAngle = mapDirection.ToAngle();

        // Same parenting rule as the server: on a grid where there is one, so the copy rides the
        // grid instead of being left behind by it.
        var fromEnt = MapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(_maps.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);

        var first = Spawn(proto, fromEnt);

        // Not a projectile, so on the server this would be thrown rather than shot. Throwing is not
        // predicted here and a thrown item is not recorded in spawnedProjectiles either, so there is
        // nothing to pair and the copy would only be a duplicate item on screen.
        if (!HasComp<ProjectileComponent>(first))
        {
            Del(first);
            return;
        }

        // Mirror the server's spread handling, pellet for pellet. The counts have to match: the
        // server pairs its Nth projectile with the Nth id reported here, so one missing pellet
        // would misalign every id after it.
        if (TryComp<ProjectileSpreadComponent>(first, out var ammoSpread) && ammoSpread.Count > 1)
        {
            var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpread.Spread);
            RaiseLocalEvent(gun, ref spreadEvent);

            var angles = PredictedLinearSpread(mapAngle - spreadEvent.Spread / 2,
                mapAngle + spreadEvent.Spread / 2,
                ammoSpread.Count);

            Launch(first, angles[0].ToVec());

            for (var i = 1; i < ammoSpread.Count; i++)
            {
                Launch(Spawn(ammoSpread.Proto, fromEnt), angles[i].ToVec());
            }
        }
        else
        {
            Launch(first, mapDirection);
        }

        void Launch(EntityUid uid, Vector2 launchDirection)
        {
            EnsureComp<PredictedProjectileClientComponent>(uid);

            // The copy has to keep simulating through prediction replays, or it stalls at the barrel.
            Physics.UpdateIsPredicted(uid);

            if (gun.Comp.Target is { } target && !TerminatingOrDeleted(target))
            {
                // Not dirtied: this entity is client-side, so there is nothing to send state for.
                var targeted = EnsureComp<TargetedProjectileComponent>(uid);
                targeted.Target = target;
            }

            ShootProjectile(uid, launchDirection, gunVelocity, gun, user, gun.Comp.ProjectileSpeedModified, toMap);
            spawnedProjectiles.Add(uid);
        }
    }

    /// <summary>
    ///     Client-side copy of the server's <c>LinearSpread</c>, so predicted pellets fan out at the
    ///     same angles the server will use.
    /// </summary>
    /// <remarks>Callers must pass <paramref name="intervals"/> greater than one.</remarks>
    private static Angle[] PredictedLinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }
    // Omu end

    private void Recoil(EntityUid? user, Vector2 recoil, float recoilScalar)
    {
        if (!Timing.IsFirstTimePredicted || user == null || recoil == Vector2.Zero || recoilScalar == 0)
            return;

        _recoil.KickCamera(user.Value, recoil.Normalized() * 0.5f * recoilScalar);
    }

    protected override void Popup(string message, EntityUid? uid, EntityUid? user)
    {
        if (uid == null || user == null || !Timing.IsFirstTimePredicted)
            return;

        PopupSystem.PopupEntity(message, uid.Value, user.Value);
    }

    protected override void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? tracked = null)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        // EntityUid check added to stop throwing exceptions due to https://github.com/space-wizards/space-station-14/issues/28252
        // TODO: Check to see why invalid entities are firing effects.
        if (gunUid == EntityUid.Invalid)
        {
            Log.Debug($"Invalid Entity sent MuzzleFlashEvent (proto: {message.Prototype}, gun: {ToPrettyString(gunUid)})");
            return;
        }

        var gunXform = Transform(gunUid);
        var gridUid = gunXform.GridUid;
        EntityCoordinates coordinates;

        if (TryComp(gridUid, out MapGridComponent? mapGrid))
        {
            coordinates = new EntityCoordinates(gridUid.Value, _maps.LocalToGrid(gridUid.Value, mapGrid, gunXform.Coordinates));
        }
        else if (gunXform.MapUid != null)
        {
            coordinates = new EntityCoordinates(gunXform.MapUid.Value, TransformSystem.GetWorldPosition(gunXform));
        }
        else
        {
            return;
        }

        var ent = Spawn(message.Prototype, coordinates);
        TransformSystem.SetWorldRotationNoLerp(ent, message.Angle);

        if (tracked != null)
        {
            var track = EnsureComp<TrackUserComponent>(ent);
            track.User = tracked;
            track.Offset = Vector2.UnitX / 2f;
        }

        var lifetime = 0.4f;

        if (TryComp<TimedDespawnComponent>(gunUid, out var despawn))
        {
            lifetime = despawn.Lifetime;
        }

        var anim = new Animation()
        {
            Length = TimeSpan.FromSeconds(lifetime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Color.White.WithAlpha(1f), 0),
                        new AnimationTrackProperty.KeyFrame(Color.White.WithAlpha(0f), lifetime)
                    }
                }
            }
        };

        _animPlayer.Play(ent, anim, "muzzle-flash");
        if (!TryComp(gunUid, out PointLightComponent? light))
        {
            light = Factory.GetComponent<PointLightComponent>();
            light.NetSyncEnabled = false;
            AddComp(gunUid, light);
        }

        Lights.SetEnabled(gunUid, true, light);
        Lights.SetRadius(gunUid, 2f, light);
        Lights.SetColor(gunUid, Color.FromHex("#cc8e2b"), light);
        Lights.SetEnergy(gunUid, 5f, light);

        var animTwo = new Animation()
        {
            Length = TimeSpan.FromSeconds(lifetime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(PointLightComponent),
                    Property = nameof(PointLightComponent.Energy),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(5f, 0),
                        new AnimationTrackProperty.KeyFrame(0f, lifetime)
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(PointLightComponent),
                    Property = nameof(PointLightComponent.AnimatedEnable),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(true, 0),
                        new AnimationTrackProperty.KeyFrame(false, lifetime)
                    }
                }
            }
        };

        var uidPlayer = EnsureComp<AnimationPlayerComponent>(gunUid);

        _animPlayer.Stop(gunUid, uidPlayer, "muzzle-flash-light");
        _animPlayer.Play((gunUid, uidPlayer), animTwo, "muzzle-flash-light");
    }

    /// <remarks>We use our own sorting algorithm separate from the default for smarter configurability.</remarks>
    private NetEntity? GetBestTarget(IEye eye, MapCoordinates coordinates)
    {
        // Find all the entities intersecting our click
        var entities = _spriteTree.QueryAabb(coordinates.MapId, Box2.CenteredAround(coordinates.Position, new Vector2(1, 1)));

        // Check the entities against whether or not we can click them
        var foundEntities = new List<(EntityUid, bool, bool, int, uint, float, float)>(entities.Count);

        foreach (var entity in entities)
        {
            // Don't add the target if we can't shoot the target!
            if (!CheckFixtures(entity.Uid))
                continue;

            var entry = CheckTarget((entity.Uid, entity.Component, entity.Transform), eye, coordinates);
            foundEntities.Add(entry);
        }

        if (foundEntities.Count == 0)
            return null;

        // Do drawdepth & y-sorting. First index is the top-most sprite (opposite of normal render order).
        foundEntities.Sort(_comparer);
        var (target, alive, occluded, _, _, _, _) = foundEntities.FirstOrDefault();

        // Prevents us from just selecting a random target nearby our cursor. It must either be alive, or our cursor must be on top of it!
        if (!occluded && !alive)
            return null;

        return GetNetEntity(target);
    }

    private (EntityUid, bool, bool, int, uint, float, float) CheckTarget(Entity<SpriteComponent, TransformComponent> target, IEye eye, MapCoordinates coordinates)
    {
        var occluded = _clickable.CheckClick((target.Owner, null, target.Comp1, target.Comp2),
            coordinates.Position,
            eye,
            true,
            out var drawDepthClicked,
            out var renderOrder,
            out var bottom);

        var difference = (target.Comp2.Coordinates.Position - coordinates.Position).LengthSquared();

        return (target.Owner, _mobState.IsAlive(target.Owner), occluded, drawDepthClicked, renderOrder, bottom, difference);
    }

    /// <summary>
    /// This Comparer takes a list of Entities that we can hit and orders them by which target the player is probably trying to shoot.
    /// We organize based on these criteria in this order:
    /// alive means the entity has a MobState and is currently alive. We check it first since they typically shoot back.
    /// occluded is whether the cursor is above the sprite or just near it.
    /// depth is the order in which sprites are layered, bigger number means its rendered above others.
    /// renderOrder is used to indicate if a sprite should be visually more important, typically this value is 0.
    /// bottom indicates which sprite is visually the lowest on the screen and therefore typically above other sprites.
    /// distance indicates the distance from the entity's coordinates to our mouse.
    /// If all of those tie, then we organize by whichever entity has the highest EntityUid.
    /// </summary>
    private sealed class GunTargetEntityComparer : IComparer<(EntityUid clicked, bool alive, bool occluded, int depth, uint renderOrder, float bottom, float distance)>
    {
        public int Compare((EntityUid clicked, bool alive, bool occluded, int depth, uint renderOrder, float bottom, float distance) x,
            (EntityUid clicked, bool alive, bool occluded, int depth, uint renderOrder, float bottom, float distance) y)
        {
            var cmp = y.occluded.CompareTo(x.occluded);

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = y.alive.CompareTo(x.alive);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = y.depth.CompareTo(x.depth);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = y.renderOrder.CompareTo(x.renderOrder);

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = -y.bottom.CompareTo(x.bottom);

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = -y.distance.CompareTo(x.distance);

            if (cmp != 0)
            {
                return cmp;
            }

            return y.clicked.CompareTo(x.clicked);
        }
    }

    private bool CheckFixtures(Entity<FixturesComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        // TODO: Maybe also check that our cursor is intersecting a valid fixture?
        foreach (var fix in entity.Comp.Fixtures)
        {
            if (!fix.Value.Hard || (fix.Value.CollisionLayer & (int)CollisionGroup.BulletImpassable) == 0)
                continue;

            // Only need to check if we're hitting one fixture
            return true;
        }

        // If we cannot collide then we absolutely do not want to target it!
        return false;
    }

    public override void PlayImpactSound(EntityUid otherEntity, DamageSpecifier? modifiedDamage, SoundSpecifier? weaponSound, bool forceWeaponSound) { }
}

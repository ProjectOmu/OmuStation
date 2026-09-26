// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Omu.Common.CCVar;
using Content.Server._RMC14.Movement;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using TestPair = Content.IntegrationTests.Pair.TestPair;

namespace Content.IntegrationTests.Tests._Omu;

/// <summary>
/// Phase 2 of the gun prediction port lets a client spawn its own copy of a bullet and then tell the
/// server "my copy hit that entity". That is a client-trust surface: for the first time a client's
/// claim can cause damage. These tests pin the two properties that make it safe to ship -
/// <b>it is off unless someone turns it on</b>, and <b>when it is on, a client cannot claim a hit
/// it could not plausibly have seen</b>.
/// </summary>
/// <remarks>
/// <para>
/// The feature ships disabled (<c>omu.gun_prediction</c> defaults to false), so
/// <see cref="PredictionIsOffByDefault"/> is the test that matters most on a normal server: it is the
/// one that fails if someone flips the default without meaning to.
/// </para>
/// </remarks>
[TestFixture]
public sealed class GunPredictionTest
{
    private const string Shooter = "OmuGunPredTestShooter";
    private const string Target = "OmuGunPredTestTarget";
    private const string Gun = "OmuGunPredTestGun";
    private const string Bullet = "OmuGunPredTestBullet";
    private const string Wall = "OmuGunPredTestWall";
    private const string CoveredTarget = "OmuGunPredTestCoveredTarget";
    private const string Bow = "OmuGunPredTestBow";

    /// <summary>Damage of a single <see cref="Bullet"/>, per its prototype below.</summary>
    private const float BulletDamage = 5f;

    /// <summary>
    /// How far from the target a bullet is parked when a test wants a predicted hit to be accepted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sized against the Path B adjudicator (<c>SharedRMCLagCompensationSystem.Collides</c>), which
    /// is an AABB-vs-AABB overlap plus <c>omu.lag_compensation_margin_tiles</c> (0.25) of slack. The
    /// target's fixture is a 0.35-radius circle and the bullet's a 0.15 half-extent box, so:
    /// </para>
    /// <list type="bullet">
    ///   <item>the AABBs overlap below 0.50 tiles of centre separation;</item>
    ///   <item>the margin still accepts out to 0.50 + 0.25 = <b>0.75</b> tiles;</item>
    ///   <item>beyond that a claim is refused.</item>
    /// </list>
    /// <para>
    /// 0.6 sits inside the margin but outside AABB overlap, so an accepted claim here is the margin
    /// doing its job rather than the two simply being on top of each other. Note this is far tighter
    /// than the 1.0 that the previous Path A adjudicator accepted - that difference is the point of
    /// the migration, and this constant failing is the expected symptom if anyone reverts it.
    /// </para>
    /// <para>
    /// Physics is kept out of these tests by <see cref="Freeze"/>, which disables collision on the
    /// projectile outright, so no assertion here can be satisfied by an ordinary contact.
    /// </para>
    /// </remarks>
    private static readonly Vector2 NearMissOffset = new(0f, 0.6f);

    /// <summary>
    /// A deliberately spread-free, slow gun firing a weak bullet. Slow so the projectile is still
    /// alive and mid-flight when the assertions run; spread-free so no assertion depends on RNG.
    /// </summary>
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OmuGunPredTestShooter
  components:
  - type: Transform
  - type: CombatMode

- type: entity
  id: OmuGunPredTestTarget
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.35
        hard: true
        layer:
        - MobLayer
        mask:
        - MobMask
  - type: Damageable
  - type: LagCompensation

- type: entity
  id: OmuGunPredTestBullet
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      # Must be named ""projectile"": that is SharedProjectileSystem.ProjectileFixture, and the
      # predicted-hit path requires one (IsCollisionPrevented has to name a real projectile fixture
      # to re-raise PreventCollideEvent with, and fails closed without it). Physics is kept out of
      # these tests by DISTANCE instead - see NearMissOffset.
      projectile:
        shape: !type:PhysShapeAabb
          bounds: ""-0.15,-0.15,0.15,0.15""
        hard: false
        mask:
        - Impassable
        - BulletImpassable
  - type: Projectile
    damage:
      types:
        Blunt: 5

- type: entity
  id: OmuGunPredTestCoveredTarget
  parent: OmuGunPredTestTarget
  components:
  # Active means a projectile passes over this entity unless it was aimed at it - the prone and
  # behind-cover rule. RequireProjectileTargetSystem enforces it by cancelling PreventCollideEvent.
  - type: RequireProjectileTarget
    active: true

- type: entity
  id: OmuGunPredTestWall
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      # Deliberately THIN (0.2 tiles). It has to sit between the bullet and the target without
      # physically touching the bullet: if the bullet collides with the wall it marks itself spent,
      # and the predicted claim is then refused by the ProjectileSpent guard rather than by the
      # line-of-sight test this fixture exists to prove. Verified by mutation - with HasClearShot
      # disabled and a full-tile wall, this test still passed.
      fix1:
        shape: !type:PhysShapeAabb
          bounds: ""-0.5,-0.1,0.5,0.1""
        hard: true
        layer:
        - WallLayer
        mask:
        - WallLayer

- type: entity
  id: OmuGunPredTestArrow
  parent: OmuGunPredTestBullet
  components:
  # Stands in for arrows, harpoons and syringe darts: anything that embeds or lands and stays in the
  # world rather than being deleted on hit.
  - type: EmbeddableProjectile
    embedOnThrow: false

- type: entity
  id: OmuGunPredTestBow
  parent: OmuGunPredTestGun
  components:
  - type: BasicEntityAmmoProvider
    proto: OmuGunPredTestArrow
    capacity: 20
    count: 20

- type: entity
  id: OmuGunPredTestGun
  components:
  - type: Gun
    fireRate: 2
    minAngle: 0
    maxAngle: 0
    angleIncrease: 0
    angleDecay: 0
    projectileSpeed: 5
  - type: BasicEntityAmmoProvider
    proto: OmuGunPredTestBullet
    capacity: 20
    count: 20
";

    /// <summary>
    /// The kill switch. With the CVar at its shipping default, firing a gun must produce exactly the
    /// pre-port result: a plain server projectile with no prediction bookkeeping attached to it.
    /// </summary>
    /// <remarks>
    /// If this fails, the port is live on every server that has not opted in, and the client-trust
    /// surface below is reachable by default. That is the single most important thing these tests
    /// protect.
    /// </remarks>
    [Test]
    public async Task PredictionIsOffByDefault()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();

        // Assert the SHIPPING default, not cfg.GetCVar - PoolManager.Cvars pins this CVar to false
        // for the whole suite, so reading it back would assert the harness and pass even if the
        // shipping default were flipped to true.
        Assert.That(OmuCVars.GunPrediction.DefaultValue, Is.False,
            "omu.gun_prediction must ship disabled. If this default is being changed deliberately, " +
            "that is a gameplay and trust decision that needs a human, not a test edit.");
        Assert.That(cfg.GetCVar(OmuCVars.GunPrediction), Is.False, "Test harness precondition.");

        // A NON-EMPTY id list, deliberately. With null or empty ids the pairing handler early-returns
        // before it ever consults the CVar, so the test would pass with the kill switch deleted.
        var fired = await FireOnce(pair, new List<int> { 1234 });

        Assert.That(fired, Is.Not.Empty, "The test gun spawned no projectile, so the test proves nothing.");
        Assert.That(fired.Any(x => entMan.HasComponent<PredictedProjectileServerComponent>(x)), Is.False,
            "A projectile was tagged for client prediction while prediction is disabled.");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// With prediction enabled, a server projectile must carry the id of the client-side copy the
    /// shooter drew for it, so the client can retire its copy when the real one dies.
    /// </summary>
    [Test]
    public async Task ServerProjectileIsPairedWithClientId()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();

        await server.WaitPost(() => cfg.SetCVar(OmuCVars.GunPrediction, true));

        const int clientId = 4242;
        var fired = await FireOnce(pair, new List<int> { clientId });

        Assert.That(fired, Is.Not.Empty);

        await server.WaitAssertion(() =>
        {
            var tagged = fired.Where(x => entMan.HasComponent<PredictedProjectileServerComponent>(x)).ToList();
            Assert.That(tagged, Is.Not.Empty,
                "Prediction is on and a player fired, but no projectile was paired with the client's copy.");

            var comp = entMan.GetComponent<PredictedProjectileServerComponent>(tagged[0]);
            Assert.That(comp.ClientId, Is.EqualTo(clientId),
                "The projectile was paired with the wrong client id, so the client would retire the wrong copy.");
            Assert.That(comp.Shooter?.UserId, Is.EqualTo(pair.Player!.UserId),
                "The projectile was attributed to the wrong session.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Malformed client input must not throw or mis-pair. The id list is attacker-controlled: it can
    /// be null, shorter than the number of shots the server actually fired, or longer.
    /// </summary>
    [Test]
    [TestCase(null, TestName = "MalformedPredictedIds_Null")]
    [TestCase(new int[] { }, TestName = "MalformedPredictedIds_Empty")]
    [TestCase(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, TestName = "MalformedPredictedIds_TooMany")]
    public async Task MalformedPredictedIdsAreSurvived(int[]? ids)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        await server.WaitPost(() => cfg.SetCVar(OmuCVars.GunPrediction, true));

        // The assertion is the absence of a thrown exception or a logged error; the integration
        // harness fails the test on any log at or above RTCVars.FailureLogLevel.
        var fired = await FireOnce(pair, ids?.ToList());
        Assert.That(fired, Is.Not.Empty, "The gun did not fire, so the malformed input was never reached.");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A client may only claim a hit at a position the server can still believe it saw. Claiming one
    /// against a target nowhere near the bullet must deal no damage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the test for the trust surface the whole feature introduces, so it carries its own
    /// positive control: the same claim, made when the bullet really is on top of the target, must
    /// land. Without that control a wiring mistake that ignores predicted hits entirely would make
    /// the rejection half pass for the wrong reason.
    /// </para>
    /// </remarks>
    [Test]
    public async Task ImplausiblePredictedHitIsRejected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var netMan = pair.Client.ResolveDependency<IEntityNetworkManager>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitPost(() => cfg.SetCVar(OmuCVars.GunPrediction, true));

        // 1. The lie: the bullet is at the origin, the target is five tiles away, and the client
        //    claims they met. Five tiles is far outside both the coordinate deviation (1 tile) and
        //    the AABB enlargement (1.5 tiles), so no amount of legitimate rewinding reaches it.
        const int lyingId = 777;
        var scene = await SetUpShot(pair, new List<int> { lyingId });
        Assert.That(scene.Projectiles, Is.Not.Empty, "The gun did not fire, so nothing was adjudicated.");

        MapCoordinates claimedPos = default;
        await server.WaitPost(() => claimedPos = xformSys.GetMapCoordinates(scene.Target));

        await pair.Client.WaitPost(() => netMan.SendSystemNetworkMessage(
            new PredictedProjectileHitEvent(lyingId,
                new HashSet<(NetEntity, MapCoordinates)> { (entMan.GetNetEntity(scene.Target), claimedPos) })));

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<DamageableComponent>(scene.Target).TotalDamage.Float(), Is.Zero,
                "The server accepted a hit claimed five tiles away from where the bullet was. " +
                "A client could deal damage without aiming."));

        // 2. The control. This MUST use a fresh projectile and a fresh id: ProcessPredictedHit sets
        //    PredictedProjectileServerComponent.Hit before it adjudicates, and nothing ever resets it,
        //    so re-claiming id 777 would be refused by that latch rather than by the geometry - and the
        //    control would "fail" for a reason that has nothing to do with what it is controlling for.
        const int honestId = 778;
        var second = await FireAgain(pair, scene, honestId);
        Assert.That(second, Is.Not.Empty, "The second shot did not fire, so there is no control.");

        await server.WaitPost(() => ParkNear(entMan, second[0], claimedPos));
        await pair.RunTicksSync(1);

        await pair.Client.WaitPost(() => netMan.SendSystemNetworkMessage(
            new PredictedProjectileHitEvent(honestId,
                new HashSet<(NetEntity, MapCoordinates)> { (entMan.GetNetEntity(scene.Target), claimedPos) })));

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<DamageableComponent>(scene.Target).TotalDamage.Float(), Is.GreaterThan(0f),
                "A legitimate predicted hit was rejected, so the rejection above proves nothing - " +
                "the server may simply be ignoring predicted hits altogether. Note the bullet is " +
                "parked NearMissOffset away, too far for physics to have caused this."));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A claimed hit must have been reachable. Proximity alone is not enough: the acceptance box
    /// around a target is over a tile wide in each direction, so without a line-of-sight test a
    /// client can claim a hit on someone standing behind a wall beside the bullet's path.
    /// </summary>
    [Test]
    public async Task PredictedHitThroughAWallIsRejected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var netMan = pair.Client.ResolveDependency<IEntityNetworkManager>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitPost(() => cfg.SetCVar(OmuCVars.GunPrediction, true));

        const int id = 900;
        var scene = await SetUpShot(pair, new List<int> { id });
        Assert.That(scene.Projectiles, Is.Not.Empty);

        MapCoordinates targetPos = default;
        await server.WaitPost(() =>
        {
            targetPos = xformSys.GetMapCoordinates(scene.Target);

            // Wall directly between the bullet and the target, and the bullet close enough that a
            // pure proximity test would accept the claim.
            // Bullet parked at the same near-miss distance that ImplausiblePredictedHitIsRejected's
            // control proves is ACCEPTED, with a wall interposed. So the only difference between this
            // test and that passing control is the wall, which is exactly what it must isolate.
            entMan.SpawnEntity(Wall, new MapCoordinates(targetPos.Position + NearMissOffset / 2f, targetPos.MapId));
            ParkNear(entMan, scene.Projectiles[0], targetPos);
        });

        await pair.RunTicksSync(2);

        await pair.Client.WaitPost(() => netMan.SendSystemNetworkMessage(
            new PredictedProjectileHitEvent(id,
                new HashSet<(NetEntity, MapCoordinates)> { (entMan.GetNetEntity(scene.Target), targetPos) })));

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<DamageableComponent>(scene.Target).TotalDamage.Float(), Is.Zero,
                "The server accepted a predicted hit on a target with a wall between it and the bullet."));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Both halves of the adjudication must answer about the same target position - the one the
    /// shooter's client last saw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The line-of-sight test used to raycast to the target's <i>live</i>
    /// position while the overlap test accepted on its <i>rewound</i> one, so the two disagreed by
    /// exactly the rewind distance and either could overrule the other. This is the false-negative
    /// half: a target that stood in the open when the shooter fired and ran behind cover afterwards
    /// had a legitimate hit refused by a wall that was never between the bullet and anything the
    /// shooter actually saw.
    /// </para>
    /// <para>
    /// The wall is placed on the far side of the bullet from the rewound position, so it obstructs
    /// only the live line. Mutation check: point HasClearShot's destination back at the live
    /// position and this test fails, because the ray then crosses the wall.
    /// </para>
    /// </remarks>
    [Test]
    public async Task PredictedHitUsesTheRewoundPositionForLineOfSight()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var netMan = pair.Client.ResolveDependency<IEntityNetworkManager>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var lagCompSys = entMan.System<RMCLagCompensationSystem>();

        await server.WaitPost(() => cfg.SetCVar(OmuCVars.GunPrediction, true));

        const int id = 910;
        var scene = await SetUpShot(pair, new List<int> { id });
        Assert.That(scene.Projectiles, Is.Not.Empty);

        var session = pair.Player;
        Assert.That(session, Is.Not.Null, "Test needs a connected player to have a perspective to rewind to.");

        // Geometry, all on one vertical line through the target's spawn column:
        //
        //   y = +2.0   target, live       (behind the wall, from the bullet's point of view)
        //   y = +0.5   wall               (thin, horizontal)
        //   y = -0.4   bullet, parked
        //   y = -1.0   target, rewound    (clear line from the bullet, opposite side)
        //
        // So the wall obstructs the live line and only the live line.
        var mapId = MapId.Nullspace;
        await server.WaitPost(() => mapId = xformSys.GetMapCoordinates(scene.Target).MapId);

        var seenPosition = new Vector2(5f, -1f);
        var bulletPosition = seenPosition + NearMissOffset;
        var wallPosition = bulletPosition + new Vector2(0f, 0.9f);
        var livePosition = bulletPosition + new Vector2(0f, 2.4f);

        var seenPos = new MapCoordinates(seenPosition, mapId);

        // The target has to ARRIVE at the seen position, not merely be spawned there: LagCompensation
        // records a position when the entity moves, so a target that never moved has an empty history
        // and every rewind resolves to its live position. HitscanHitsTargetWhereTheShooterSawIt does
        // the same thing for the same reason.
        var seenTick = GameTick.Zero;
        await server.WaitPost(() =>
        {
            xformSys.SetWorldPosition(scene.Target, seenPosition);
            seenTick = timing.CurTick;
            ParkNear(entMan, scene.Projectiles[0], seenPos);
        });

        await pair.RunTicksSync(2);

        // The target runs past the bullet and stops behind cover.
        await server.WaitPost(() =>
        {
            entMan.SpawnEntity(Wall, new MapCoordinates(wallPosition, mapId));
            xformSys.SetWorldPosition(scene.Target, livePosition);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            lagCompSys.SetLastRealTick(session!.UserId, seenTick);

            Assert.That(lagCompSys.GetLastRealTick(session.UserId), Is.EqualTo(seenTick),
                "The clamp in SetLastRealTick rejected a legitimate tick, so nothing below is rewound.");

            // Guard against a vacuous pass: the whole test rests on these two positions differing,
            // with the wall between the bullet and exactly one of them.
            var rewound = xformSys.ToMapCoordinates(lagCompSys.GetCoordinates(scene.Target, session)).Position;

            Assert.That((rewound - seenPosition).Length(), Is.LessThan(0.1f),
                "The target did not rewind to where the shooter saw it, so this test is not " +
                "exercising the disagreement it exists to pin.");

            Assert.That((xformSys.GetWorldPosition(scene.Target) - livePosition).Length(), Is.LessThan(0.1f),
                "The target is not where the test moved it.");

            Assert.That(rewound.Y, Is.LessThan(bulletPosition.Y).And.LessThan(wallPosition.Y),
                "The wall must sit past the bullet, on the far side from the rewound position.");
        });

        await pair.Client.WaitPost(() => netMan.SendSystemNetworkMessage(
            new PredictedProjectileHitEvent(id,
                new HashSet<(NetEntity, MapCoordinates)> { (entMan.GetNetEntity(scene.Target), seenPos) })));

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<DamageableComponent>(scene.Target).TotalDamage.Float(), Is.GreaterThan(0),
                "The server refused a hit that was unobstructed from where the shooter saw the target. " +
                "Line of sight is being tested against the live position while acceptance uses the " +
                "rewound one - the two halves of the adjudication disagree."));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A projectile that embeds must never be paired for prediction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pairing hides the real projectile from its shooter, and only a paired projectile being deleted
    /// shows it again. A bullet is deleted on hit, so that is fine. An arrow embeds in its target, or
    /// lands, and stays in the world - so a paired arrow stayed invisible to whoever fired it, while
    /// the client's own copy, which embeds too, was never retired and lingered as a phantom. This is
    /// the failure bows and syringe guns historically hit after RMC-style prediction was added.
    /// </para>
    /// <para>
    /// The control half fires an ordinary bullet in the same test and requires it to be paired, so
    /// this cannot pass merely because pairing is broken for everything.
    /// </para>
    /// </remarks>
    [Test]
    public async Task EmbeddableProjectilesAreNotPaired()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var gunSys = entMan.System<GunSystem>();

        await server.WaitPost(() => cfg.SetCVar(OmuCVars.GunPrediction, true));

        List<EntityUid>? fired = null;
        await server.WaitPost(() =>
        {
            var mapUid = mapSys.CreateMap(out var mapId);
            var shooter = entMan.SpawnEntity(Shooter, new MapCoordinates(Vector2.Zero, mapId));
            var bow = entMan.SpawnEntity(Bow, new MapCoordinates(Vector2.Zero, mapId));
            server.PlayerMan.SetAttachedEntity(pair.Player!, shooter);

            var gunComp = entMan.GetComponent<GunComponent>(bow);
            gunComp.ShootCoordinates = new EntityCoordinates(mapUid, new Vector2(5f, 0f));
            fired = gunSys.AttemptShoot(shooter, (bow, gunComp), new List<int> { 930 }, pair.Player);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(fired, Is.Not.Null, "The bow did not fire at all, so this test proves nothing.");

            var arrows = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<EmbeddableProjectileComponent, ProjectileComponent>();
            while (query.MoveNext(out var uid, out _, out _))
            {
                arrows.Add(uid);
            }

            Assert.That(arrows, Is.Not.Empty, "No arrow is in flight, so this test proves nothing.");
            Assert.That(arrows.Where(x => entMan.HasComponent<PredictedProjectileServerComponent>(x)), Is.Empty,
                "An embeddable projectile was paired for prediction. Its shooter would never see it again " +
                "once it embedded or landed.");
            Assert.That(fired, Is.Empty,
                "AttemptShoot listed an embeddable projectile. The client never reports an id for one, so " +
                "listing it here would misalign every later projectile in the shot.");
        });

        // Control: an ordinary bullet in the same conditions must still be paired.
        var bullets = await FireOnce(pair, new List<int> { 931 });
        await server.WaitAssertion(() =>
            Assert.That(bullets.Where(x => entMan.HasComponent<PredictedProjectileServerComponent>(x)), Is.Not.Empty,
                "An ordinary bullet was not paired either, so the check above proves nothing."));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// One hit report must not damage the same target twice, even for a projectile that deliberately
    /// survives its own hit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The obvious limiter - the projectile marking itself spent on the first hit - does not hold for
    /// penetrating projectiles: the Omu hardlight-bow block in <c>ProjectileSystem.ProjectileCollide</c>
    /// resets <c>ProjectileSpent</c> back to false, and so does Goobstation's <c>TryPenetrate</c> when
    /// it succeeds. So a report naming the same victim repeatedly, or many victims at once, needs an
    /// explicit bound rather than relying on the projectile to stop itself.
    /// </para>
    /// <para>
    /// This uses an ordinary projectile and simply names the same target several times in one report;
    /// the per-report dedupe is what must hold, independently of whether the projectile penetrates.
    /// </para>
    /// </remarks>
    [Test]
    public async Task RepeatedClaimsInOneReportDamageOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var netMan = pair.Client.ResolveDependency<IEntityNetworkManager>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitPost(() => cfg.SetCVar(OmuCVars.GunPrediction, true));

        const int id = 901;
        var scene = await SetUpShot(pair, new List<int> { id });
        Assert.That(scene.Projectiles, Is.Not.Empty);

        MapCoordinates targetPos = default;
        await server.WaitPost(() =>
        {
            targetPos = xformSys.GetMapCoordinates(scene.Target);
            ParkNear(entMan, scene.Projectiles[0], targetPos);
        });

        await pair.RunTicksSync(1);

        // The same victim named three times, at three positions all inside the tolerance. A HashSet
        // of (NetEntity, MapCoordinates) does not collapse these - the coordinates differ.
        var netTarget = entMan.GetNetEntity(scene.Target);
        await pair.Client.WaitPost(() => netMan.SendSystemNetworkMessage(
            new PredictedProjectileHitEvent(id,
                new HashSet<(NetEntity, MapCoordinates)>
                {
                    (netTarget, targetPos),
                    (netTarget, new MapCoordinates(targetPos.Position + new Vector2(0.05f, 0f), targetPos.MapId)),
                    (netTarget, new MapCoordinates(targetPos.Position + new Vector2(0f, 0.05f), targetPos.MapId)),
                })));

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var damage = entMan.GetComponent<DamageableComponent>(scene.Target).TotalDamage.Float();
            Assert.That(damage, Is.GreaterThan(0f), "The honest first claim should still have landed.");
            Assert.That(damage, Is.LessThanOrEqualTo(BulletDamage),
                $"One report dealt {damage} damage from a single {BulletDamage}-damage bullet, so the " +
                "same target was adjudicated more than once.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A target the engine's own collision filtering refuses must not become hittable just because a
    /// client says it was hit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>RequireProjectileTargetComponent</c> is how this game makes a prone player, or one behind a
    /// table, un-hittable by fire that was not aimed at them: the system cancels
    /// <c>PreventCollideEvent</c> and no contact is ever produced.
    /// </para>
    /// <para>
    /// The predicted-hit path calls <c>ProjectileCollide</c> directly, which is entered <i>after</i>
    /// the point where the engine consults those subscribers. So unless the adjudicator re-derives
    /// that filtering itself, every rule built on <c>PreventCollideEvent</c> - cover, prone,
    /// <c>IgnoredEntities</c>, <c>IgnoreShooter</c> - is bypassed by anyone with prediction on. This
    /// test is the guard on that, and it is the only test covering
    /// <c>GunPredictionSystem.IsCollisionPrevented</c>.
    /// </para>
    /// </remarks>
    [Test]
    public async Task PredictedHitOnCoveredTargetIsRejected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var netMan = pair.Client.ResolveDependency<IEntityNetworkManager>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitPost(() => cfg.SetCVar(OmuCVars.GunPrediction, true));

        const int id = 902;
        var scene = await SetUpShot(pair, new List<int> { id });
        Assert.That(scene.Projectiles, Is.Not.Empty);

        var covered = EntityUid.Invalid;
        MapCoordinates coveredPos = default;

        await server.WaitPost(() =>
        {
            // Same geometry as the accepted control in ImplausiblePredictedHitIsRejected - the only
            // difference is that this target is under cover. So a rejection here is attributable to
            // the cover rule and nothing else.
            var targetPos = xformSys.GetMapCoordinates(scene.Target);
            coveredPos = new MapCoordinates(targetPos.Position + new Vector2(3f, 0f), targetPos.MapId);
            covered = entMan.SpawnEntity(CoveredTarget, coveredPos);

            ParkNear(entMan, scene.Projectiles[0], coveredPos);
        });

        await pair.RunTicksSync(2);

        await pair.Client.WaitPost(() => netMan.SendSystemNetworkMessage(
            new PredictedProjectileHitEvent(id,
                new HashSet<(NetEntity, MapCoordinates)> { (entMan.GetNetEntity(covered), coveredPos) })));

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
            Assert.That(entMan.GetComponent<DamageableComponent>(covered).TotalDamage.Float(), Is.Zero,
                "A predicted hit was accepted against a target the engine's own collision filtering " +
                "refuses. Prone and behind-cover targets are hittable on demand by any client with " +
                "prediction enabled."));

        await pair.CleanReturnAsync();
    }

    private sealed record Scene(EntityUid Shooter, EntityUid Target, EntityUid Gun, List<EntityUid> Projectiles);

    /// <summary>
    /// Spawns a shooter, a target five tiles away and a gun, attaches the test player to the shooter,
    /// fires one shot, and returns what the server spawned for it.
    /// </summary>
    /// <remarks>
    /// This drives <c>AttemptShoot</c>'s prediction-aware overload directly rather than sending a real
    /// <c>RequestShootEvent</c>. Going through the net event would make every test here additionally
    /// depend on the client's predicted-spawn path, and these tests are about what the <i>server</i>
    /// does with what a client sends - including a client that never ran that path honestly.
    /// </remarks>
    private static async Task<Scene> SetUpShot(TestPair pair, List<int>? predictedIds)
    {
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var gunSys = entMan.System<GunSystem>();

        var shooter = EntityUid.Invalid;
        var target = EntityUid.Invalid;
        var gun = EntityUid.Invalid;
        var spawned = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            var mapUid = mapSys.CreateMap(out var mapId);

            shooter = entMan.SpawnEntity(Shooter, new MapCoordinates(Vector2.Zero, mapId));
            target = entMan.SpawnEntity(Target, new MapCoordinates(new Vector2(5f, 0f), mapId));
            gun = entMan.SpawnEntity(Gun, new MapCoordinates(Vector2.Zero, mapId));

            server.PlayerMan.SetAttachedEntity(pair.Player!, shooter);

            var gunComp = entMan.GetComponent<GunComponent>(gun);
            gunComp.ShootCoordinates = new EntityCoordinates(mapUid, new Vector2(5f, 0f));

            var fired = gunSys.AttemptShoot(shooter, (gun, gunComp), predictedIds, pair.Player);
            if (fired != null)
                spawned.AddRange(fired);
        });

        await pair.RunTicksSync(1);

        // Freeze whatever was fired. These tests place projectiles deliberately; a bullet still
        // travelling towards the target will physically reach it partway through a test that waits
        // for a gun cooldown, and then ANY assertion about damage is really an assertion about
        // physics. Every accepted-hit assertion in this fixture depends on that not happening.
        await server.WaitPost(() =>
        {
            foreach (var projectile in spawned)
            {
                Freeze(entMan, projectile);
            }
        });

        return new Scene(shooter, target, gun, spawned);
    }

    /// <summary>
    /// Parks a projectile <see cref="NearMissOffset"/> from <paramref name="target"/> with no velocity.
    /// </summary>
    private static void ParkNear(IEntityManager entMan, EntityUid projectile, MapCoordinates target)
    {
        entMan.System<SharedTransformSystem>().SetWorldPosition(projectile, target.Position + NearMissOffset);
        Freeze(entMan, projectile);
    }

    /// <summary>
    /// Pins a projectile in place and takes it out of the physics collision system entirely.
    /// </summary>
    /// <remarks>
    /// Both halves matter. Zeroing the velocity stops it drifting away from where a test put it.
    /// Clearing <c>CanCollide</c> is what makes every damage assertion in this fixture meaningful:
    /// the projectile can no longer produce a contact, so the ONLY route left to damaging the target
    /// is the predicted-hit path. The prediction layer calls <c>ProjectileCollide</c> directly rather
    /// than through physics, so it is unaffected.
    /// </remarks>
    private static void Freeze(IEntityManager entMan, EntityUid projectile)
    {
        if (!entMan.TryGetComponent(projectile, out PhysicsComponent? physics))
            return;

        var physicsSys = entMan.System<SharedPhysicsSystem>();
        physicsSys.SetLinearVelocity(projectile, Vector2.Zero, body: physics);
        physicsSys.SetCanCollide(projectile, false, body: physics);
    }

    /// <summary>
    /// Fires the already-set-up gun again, after its cooldown, with a fresh predicted id.
    /// </summary>
    private static async Task<List<EntityUid>> FireAgain(TestPair pair, Scene scene, int predictedId)
    {
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var gunSys = entMan.System<GunSystem>();
        var spawned = new List<EntityUid>();

        // fireRate is 2/s, so let the gun come off cooldown before asking it to fire again.
        await pair.RunTicksSync(40);

        await server.WaitPost(() =>
        {
            var gunComp = entMan.GetComponent<GunComponent>(scene.Gun);

            // The shooting core does not reset ShotCounter - only the public coordinate-taking
            // overloads do. Leaving it at 1 makes a SemiAuto gun compute `shots = 1 - 1 = 0` and
            // silently fire nothing, which looks exactly like a broken feature.
            gunComp.ShotCounter = 0;

            var fired = gunSys.AttemptShoot(scene.Shooter, (scene.Gun, gunComp), new List<int> { predictedId }, pair.Player);
            if (fired != null)
                spawned.AddRange(fired);
        });

        await pair.RunTicksSync(1);

        await server.WaitPost(() =>
        {
            foreach (var projectile in spawned)
            {
                Freeze(entMan, projectile);
            }
        });

        return spawned;
    }

    private static async Task<List<EntityUid>> FireOnce(TestPair pair, List<int>? predictedIds = null)
    {
        var scene = await SetUpShot(pair, predictedIds);
        return scene.Projectiles;
    }
}

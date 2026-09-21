// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Numerics;
using Content.Omu.Common.CCVar;
using Content.Server._RMC14.Movement;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Damage;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Omu;

/// <summary>
/// Guns must adjudicate shots against where the shooter's client was actually rendering the target,
/// not against where the server has moved it to since. Omu has shipped
/// <c>LagCompensationComponent</c> for a long time, but until the gun prediction port only melee
/// ever consumed it — a hitscan raycast ran against live physics, so shooting a strafing target
/// missed by the shooter's round-trip time.
/// </summary>
/// <remarks>
/// <para>
/// The test fires the same gun at the same coordinates twice, at a target that has since moved off
/// the line of fire, and changes exactly one thing between the two shots: whether the server has
/// been told which tick the shooter last had authoritative state for.
/// </para>
/// <list type="number">
///   <item><b>Control shot</b> — no reported tick, so there is nothing to rewind to and the shot is
///         adjudicated live. It must miss. This is what pins the assertion to lag compensation:
///         if the target were simply standing in the line of fire, the control would hit too.</item>
///   <item><b>Compensated shot</b> — the shooter's session is reported as having last seen the world
///         on the tick the target was standing in the line of fire. It must hit.</item>
/// </list>
/// <para>
/// Before the port, the second shot missed as well: the raycast had no notion of a rewound
/// position, and <c>HitscanBasicRaycastSystem</c> discarded any mutation a subscriber made to
/// <c>AttemptHitscanRaycastFiredEvent.Data</c>, so no extension point could have corrected it.
/// </para>
/// </remarks>
[TestFixture]
public sealed class RangedLagCompensationTest
{
    private const string Shooter = "OmuLagCompTestShooter";
    private const string Target = "OmuLagCompTestTarget";
    private const string Gun = "OmuLagCompTestGun";
    private const string ProneTarget = "OmuLagCompTestProneTarget";

    /// <summary>
    /// Deliberately spread-free (<c>minAngle</c>/<c>maxAngle</c>/<c>angleIncrease</c> all zero) so
    /// the shot direction is exactly the aim direction and the test is not flaky on RNG.
    /// </summary>
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OmuLagCompTestShooter
  components:
  - type: Transform

- type: entity
  id: OmuLagCompTestTarget
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
  id: OmuLagCompTestProneTarget
  parent: OmuLagCompTestTarget
  components:
  # Active means a shot passes over this entity unless it was aimed at it specifically - the prone
  # and behind-cover rule, enforced for hitscan by HitscanBasicRaycastSystem. It inherits
  # LagCompensation from the parent but never moves, so it is never a rewind candidate; its entire
  # job is to be what the live raycast stops on.
  - type: RequireProjectileTarget
    active: true

- type: entity
  id: OmuLagCompTestGun
  components:
  - type: Gun
    fireRate: 8
    minAngle: 0
    maxAngle: 0
    angleIncrease: 0
    angleDecay: 0
  - type: BasicHitscanAmmoProvider
    proto: RedLaser
";

    [Test]
    public async Task HitscanHitsTargetWhereTheShooterSawIt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var gunSys = entMan.System<GunSystem>();
        var lagCompSys = entMan.System<RMCLagCompensationSystem>();

        var session = pair.Player;
        Assert.That(session, Is.Not.Null, "Test needs a connected player to have a perspective to rewind to.");

        var mapUid = EntityUid.Invalid;
        var mapId = MapId.Nullspace;
        var shooter = EntityUid.Invalid;
        var target = EntityUid.Invalid;
        var gun = EntityUid.Invalid;

        // The shooter stands at the origin and always aims at (5, 0).
        var aimPosition = new Vector2(5f, 0f);
        var offLinePosition = new Vector2(5f, 3f);

        await server.WaitPost(() =>
        {
            mapUid = mapSys.CreateMap(out mapId);

            shooter = entMan.SpawnEntity(Shooter, new MapCoordinates(Vector2.Zero, mapId));
            server.PlayerMan.SetAttachedEntity(session!, shooter);

            // Spawn off the line of fire, so that the first thing the position history records is
            // the target *arriving* on the line rather than the target already being on it.
            target = entMan.SpawnEntity(Target, new MapCoordinates(offLinePosition, mapId));
            gun = entMan.SpawnEntity(Gun, new MapCoordinates(Vector2.Zero, mapId));
        });

        await pair.RunTicksSync(5);

        // The target steps into the line of fire. This is the frame the shooter's client sees.
        var seenTick = GameTick.Zero;
        await server.WaitPost(() =>
        {
            xformSys.SetWorldPosition(target, aimPosition);
            seenTick = timing.CurTick;
        });

        await pair.RunTicksSync(2);

        // ...and strafes straight back out of it again.
        await server.WaitPost(() => xformSys.SetWorldPosition(target, offLinePosition));

        await pair.RunTicksSync(1);

        var toCoordinates = new EntityCoordinates(mapUid, aimPosition);

        // 1. Control: no reported tick, so the server adjudicates against live positions and misses.
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(),
                Is.Zero,
                "Target should be undamaged before anything is fired.");

            Assert.That(gunSys.AttemptShoot(shooter, (gun, entMan.GetComponent<GunComponent>(gun)), toCoordinates),
                Is.True,
                "The test gun failed to fire at all, so the test proves nothing.");
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(),
                Is.Zero,
                "The target had already strafed out of the line of fire, so an uncompensated shot must miss. " +
                "If this fails the rest of the test proves nothing, because the shot would land either way.");
        });

        // Let the gun come off cooldown. The target does not move, so no new position history is
        // recorded and the rewind target stays valid.
        await pair.RunTicksSync(6);

        // 2. Compensated: tell the server which tick this shooter last had real state for, which is
        //    the tick the target was standing on the line of fire, and fire the identical shot.
        await server.WaitAssertion(() =>
        {
            var elapsed = timing.CurTick.Value - seenTick.Value;
            Assert.That(elapsed * timing.TickPeriod, Is.LessThan(lagCompSys.MaxRewindTicks() * timing.TickPeriod),
                "Test is running slower than the lag compensation buffer, so the rewind target would be culled. " +
                "Shorten the test or raise omu.lag_compensation_milliseconds.");

            lagCompSys.SetLastRealTick(session!.UserId, seenTick);

            Assert.That(lagCompSys.GetLastRealTick(session.UserId), Is.EqualTo(seenTick),
                "The clamp in SetLastRealTick rejected a legitimate tick.");

            Assert.That(gunSys.AttemptShoot(shooter, (gun, entMan.GetComponent<GunComponent>(gun)), toCoordinates),
                Is.True,
                "The test gun failed to fire the compensated shot.");
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(),
                Is.GreaterThan(0f),
                "The shot was fired at exactly where the shooter last saw the target, so lag compensation " +
                "should have rewound the target onto the line of fire and registered the hit.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The tick a client reports is an untrusted claim. It is clamped into
    /// <c>[CurTick - buffer, CurTick]</c> at the single point it is written, so no consumer can be
    /// handed a rewind depth the position history could not possibly satisfy.
    /// </summary>
    [Test]
    public async Task ReportedTickIsClamped()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        var lagCompSys = server.ResolveDependency<IEntityManager>().System<RMCLagCompensationSystem>();

        var session = pair.Player;
        Assert.That(session, Is.Not.Null);

        await server.WaitAssertion(() =>
        {
            var user = session!.UserId;
            var cur = timing.CurTick;
            var maxRewind = lagCompSys.MaxRewindTicks();
            Assert.That(maxRewind, Is.GreaterThan(0u));

            var earliest = cur.Value > maxRewind ? new GameTick(cur.Value - maxRewind) : GameTick.Zero;

            // A tick from the future cannot be honoured.
            lagCompSys.SetLastRealTick(user, new GameTick(cur.Value + 10_000));
            Assert.That(lagCompSys.GetLastRealTick(user), Is.EqualTo(cur));

            // Neither can one older than the position history we keep.
            lagCompSys.SetLastRealTick(user, GameTick.Zero);
            Assert.That(lagCompSys.GetLastRealTick(user), Is.EqualTo(earliest));

            // Substeps are bounded to one tick's worth in either direction.
            lagCompSys.SetLastRealTick(user, cur, 10_000);
            Assert.That(lagCompSys.GetLastRealSubstep(user), Is.EqualTo(lagCompSys.GetSubsteps()));

            lagCompSys.SetLastRealTick(user, cur, -10_000);
            Assert.That(lagCompSys.GetLastRealSubstep(user), Is.EqualTo(-lagCompSys.GetSubsteps()));

            // Leave the session with no rewind, so a recycled pair is not poisoned.
            lagCompSys.SetLastRealTick(user, cur);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A client may only rewind as far back as its own connection can account for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The buffer ceiling bounds how far the position history reaches, but it is the same number for
    /// everyone, so on its own it lets any client claim the deepest rewind the server can serve. The
    /// code this replaced derived the rewind from <c>Channel.Ping * 1.5</c> - a figure the server
    /// measures - so a fast client simply could not ask for a deep one. Without a plausibility bound
    /// that property was lost, and a 20 ms client could land melee and hitscan hits on where targets
    /// stood 750 ms ago.
    /// </para>
    /// <para>
    /// The suite pins <c>omu.lag_compensation_min_rewind_milliseconds</c> to the full buffer, because
    /// a loopback pair measures zero ping and every other test here would otherwise be held to the
    /// floor. This test lowers it on purpose, which is the only way to observe the bound in a
    /// zero-ping environment.
    /// </para>
    /// </remarks>
    [Test]
    public async Task PlausibilityBoundLimitsRewind()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var lagCompSys = server.ResolveDependency<IEntityManager>().System<RMCLagCompensationSystem>();

        var session = pair.Player;
        Assert.That(session, Is.Not.Null);

        const int floorMs = 100;
        await server.WaitPost(() => cfg.SetCVar(OmuCVars.LagCompensationMinRewindMilliseconds, floorMs));

        await server.WaitAssertion(() =>
        {
            var user = session!.UserId;
            var cur = timing.CurTick;
            var bufferTicks = lagCompSys.MaxRewindTicks();
            var floorTicks = (uint) (floorMs / timing.TickPeriod.TotalMilliseconds);

            Assert.That(floorTicks, Is.LessThan(bufferTicks),
                "Test is misconfigured: the floor must be tighter than the buffer or it bounds nothing.");

            // Claim the deepest rewind the buffer could serve. On a loopback connection the measured
            // ping is zero, so the floor is the whole allowance and the claim must be cut down to it.
            lagCompSys.SetLastRealTick(user, GameTick.Zero);

            var granted = cur.Value - lagCompSys.GetLastRealTick(user).Value;

            Assert.That(granted, Is.LessThanOrEqualTo(floorTicks + 1),
                $"A zero-ping session was granted {granted} ticks of rewind against a {floorTicks}-tick " +
                "allowance, so the plausibility bound is not being applied.");
            Assert.That(granted, Is.LessThan(bufferTicks),
                "The claim was served straight from the buffer ceiling, which is the behaviour this bound exists to stop.");

            // Leave the session with no rewind, so a recycled pair is not poisoned.
            lagCompSys.SetLastRealTick(user, cur);
        });

        await pair.CleanReturnAsync();
    }
    /// <summary>
    /// A client cannot deepen its own rewind by saying nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second half of the rewind bound. <see cref="PlausibilityBoundLimitsRewind"/> proves the
    /// bound is applied when a claim is <i>stored</i>, and reads it back in the same assertion - the
    /// zero-elapsed-ticks case. That is the one case the hole did not cover. The stored tick is
    /// frozen while <c>CurTick</c> advances, so if the offset is derived at consumption from an
    /// unbounded subtraction, every tick of silence buys another tick of rewind until the buffer
    /// ceiling catches it. The client chooses when to report - the heartbeat is combat-mode gated
    /// and rate-limited - so "stop reporting, wait, fire" is a free, ping-less way to reach the
    /// deepest rewind the server can serve.
    /// </para>
    /// <para>
    /// Mutation check: drop the plausibility term from <c>GetRewindOffset</c>'s clamp and this fails
    /// with the full elapsed offset.
    /// </para>
    /// </remarks>
    [Test]
    public async Task SilenceDoesNotDeepenRewind()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var lagCompSys = server.ResolveDependency<IEntityManager>().System<RMCLagCompensationSystem>();

        var session = pair.Player;
        Assert.That(session, Is.Not.Null);

        const int floorMs = 100;
        await server.WaitPost(() => cfg.SetCVar(OmuCVars.LagCompensationMinRewindMilliseconds, floorMs));

        // An honest report: this client is exactly up to date. Nothing about this claim is abusive,
        // which is the point - the rewind must not grow out of it.
        var reported = GameTick.Zero;
        await server.WaitPost(() =>
        {
            reported = timing.CurTick;
            lagCompSys.SetLastRealTick(session!.UserId, reported);
        });

        Assert.That(lagCompSys.GetLastRealTick(session!.UserId), Is.EqualTo(reported),
            "The claim was altered on the way in, so the rest of this test is not measuring silence.");

        // ...and then the client says nothing at all for a good deal longer than its allowance.
        const int silentTicks = 20;
        await pair.RunTicksSync(silentTicks);

        await server.WaitAssertion(() =>
        {
            var user = session.UserId;
            var floorTicks = (uint) (floorMs / timing.TickPeriod.TotalMilliseconds);
            var elapsed = timing.CurTick.Value - reported.Value;

            Assert.That(elapsed, Is.GreaterThan(floorTicks + 1),
                "Test is misconfigured: the silence must outlast the allowance or it bounds nothing.");
            Assert.That(floorTicks, Is.LessThan(lagCompSys.MaxRewindTicks()),
                "Test is misconfigured: the floor must be tighter than the buffer.");

            // One tick of slack for the ceiling rounding the allowance up.
            var allowed = (floorTicks + 1) * timing.TickPeriod;
            var granted = lagCompSys.GetRewindOffset(user);

            Assert.That(granted, Is.LessThanOrEqualTo(allowed),
                $"After {elapsed} ticks of silence a zero-ping session was granted {granted.TotalMilliseconds:F1} ms " +
                $"of rewind against a {floorMs} ms allowance. The bound is only being applied where the claim is " +
                "stored, so silence deepens the rewind for free.");

            Assert.That(granted, Is.LessThan(elapsed * timing.TickPeriod),
                "The rewind tracked the elapsed time exactly, which is the unbounded subtraction this test exists to stop.");

            // Leave the session with no rewind, so a recycled pair is not poisoned.
            lagCompSys.SetLastRealTick(user, timing.CurTick);
        });

        await pair.CleanReturnAsync();
    }
    /// <summary>
    /// Lag compensation must never move a hit <i>past</i> whatever the live raycast stopped on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second half of the prone/behind-cover rule. The candidate side of this system already refuses to make
    /// an active <c>RequireProjectileTarget</c> entity the hit. The blocker side did the mirror
    /// image of the same thing wrong: it treated every such entity as a non-blocker, on the grounds
    /// that the live raycast passes through them too. That is true only until the shooter
    /// <i>declares</i> one as the target, at which point the live ray stops there.
    /// </para>
    /// <para>
    /// So a shooter could aim at a downed player, have the live shot stop on them, and have the
    /// compensation pass hit straight through the body onto a strafing mob behind it - the mob
    /// taking a hit the live path never reached, and the declared target taking nothing. This system
    /// is not given the declared target, but it is given what the ray stopped on, which answers the
    /// same question.
    /// </para>
    /// <para>
    /// Mutation check: drop the <c>liveHit</c> comparison from <c>IsPathBlocked</c> and the final
    /// assertion fails with the mob taking a second helping of damage.
    /// </para>
    /// </remarks>
    [Test]
    public async Task CompensationCannotShootPastTheDeclaredTarget()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var mapSys = entMan.System<SharedMapSystem>();
        var xformSys = entMan.System<SharedTransformSystem>();
        var gunSys = entMan.System<GunSystem>();
        var lagCompSys = entMan.System<RMCLagCompensationSystem>();

        var session = pair.Player;
        Assert.That(session, Is.Not.Null, "Test needs a connected player to have a perspective to rewind to.");

        var mapUid = EntityUid.Invalid;
        var mapId = MapId.Nullspace;
        var shooter = EntityUid.Invalid;
        var mob = EntityUid.Invalid;
        var gun = EntityUid.Invalid;
        var prone = EntityUid.Invalid;

        // The shooter stands at the origin and always aims at (5, 0). The prone body goes at (3, 0),
        // between the two.
        var aimPosition = new Vector2(5f, 0f);
        var offLinePosition = new Vector2(5f, 3f);
        var covePosition = new Vector2(3f, 0f);

        await server.WaitPost(() =>
        {
            mapUid = mapSys.CreateMap(out mapId);

            shooter = entMan.SpawnEntity(Shooter, new MapCoordinates(Vector2.Zero, mapId));
            server.PlayerMan.SetAttachedEntity(session!, shooter);

            // Spawned off the line so the first thing the position history records is the mob
            // arriving on it, exactly as in HitscanHitsTargetWhereTheShooterSawIt.
            mob = entMan.SpawnEntity(Target, new MapCoordinates(offLinePosition, mapId));
            gun = entMan.SpawnEntity(Gun, new MapCoordinates(Vector2.Zero, mapId));
        });

        await pair.RunTicksSync(5);

        var seenTick = GameTick.Zero;
        await server.WaitPost(() =>
        {
            xformSys.SetWorldPosition(mob, aimPosition);
            seenTick = timing.CurTick;
        });

        await pair.RunTicksSync(2);

        await server.WaitPost(() => xformSys.SetWorldPosition(mob, offLinePosition));

        await pair.RunTicksSync(1);

        var toCoordinates = new EntityCoordinates(mapUid, aimPosition);
        var mobDamage = 0f;

        // 1. Control: with the line of fire clear, the compensated shot reaches the mob. Without this
        //    the final assertion would hold trivially on a build where compensation never fires.
        await server.WaitAssertion(() =>
        {
            lagCompSys.SetLastRealTick(session!.UserId, seenTick);

            Assert.That(lagCompSys.GetLastRealTick(session.UserId), Is.EqualTo(seenTick),
                "The clamp rejected a legitimate tick, so nothing below is compensated.");

            Assert.That(gunSys.AttemptShoot(shooter, (gun, entMan.GetComponent<GunComponent>(gun)), toCoordinates),
                Is.True,
                "The test gun failed to fire the control shot.");
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            mobDamage = entMan.GetComponent<DamageableComponent>(mob).TotalDamage.Float();

            Assert.That(mobDamage, Is.GreaterThan(0f),
                "Compensation did not reach the mob with a clear line of fire, so the block below " +
                "would prove nothing.");
        });

        // 2. Now put a body on the line and aim at it. fireRate is 8/s, so a handful of ticks clears
        //    the cooldown.
        await server.WaitPost(() => prone = entMan.SpawnEntity(ProneTarget, new MapCoordinates(covePosition, mapId)));

        await pair.RunTicksSync(6);

        await server.WaitAssertion(() =>
        {
            var elapsed = timing.CurTick.Value - seenTick.Value;
            Assert.That(elapsed, Is.LessThan(lagCompSys.MaxRewindTicks()),
                "Test is running slower than the lag compensation buffer, so the rewind target would be culled.");

            lagCompSys.SetLastRealTick(session!.UserId, seenTick);

            Assert.That(lagCompSys.GetLastRealTick(session.UserId), Is.EqualTo(seenTick),
                "The clamp rejected a legitimate tick, so the second shot is not compensated and the " +
                "test cannot distinguish the fix from a shot that simply stopped.");

            // Declaring the prone body as the target is what makes the live raycast stop on it
            // rather than pass over it.
            Assert.That(gunSys.AttemptShoot(shooter, (gun, entMan.GetComponent<GunComponent>(gun)), toCoordinates, prone),
                Is.True,
                "The test gun failed to fire the second shot.");
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            // Both assertions, always: the regression moves ONE hit from the declared target to the
            // mob, so it shows up as both a missing hit and an extra one. Reporting only the first
            // would make a real regression read as a broken test setup.
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<DamageableComponent>(mob).TotalDamage.Float(),
                    Is.EqualTo(mobDamage),
                    "The mob behind the declared target took a second hit. Lag compensation moved the " +
                    "hit PAST what the live raycast stopped on, onto a target the live shot never reached.");

                Assert.That(entMan.GetComponent<DamageableComponent>(prone).TotalDamage.Float(),
                    Is.GreaterThan(0f),
                    "The declared target took no damage. If the assertion above also failed, the hit " +
                    "was moved off it onto the mob - that is the regression. If it passed, the live " +
                    "raycast never stopped on the declared target and this test is not set up the way " +
                    "it thinks it is.");
            });
        });

        await pair.CleanReturnAsync();
    }
}

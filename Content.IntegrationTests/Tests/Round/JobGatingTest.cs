// SPDX-License-Identifier: AGPL-3.0-or-later
// Omu - added by Omu Station: regression test for the inverted playtime job-gating check in PlayTimeTrackingSystem.

#nullable enable
using System.Collections.Generic;
using Content.Server.Database;
using Content.Server.GameTicking.Events;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Round;

/// <summary>
/// Exercises the job gating logic (role timers and the job whitelist).
/// </summary>
/// <remarks>
/// <see cref="PoolManager.TestCvars"/> forces <c>game.role_timers</c>, <c>game.role_loadout_timers</c> and
/// <c>game.role_whitelist</c> to <c>false</c> for every test in the suite, which means none of the gating code
/// below is ever executed by any other test. Those defaults are deliberate (they keep the rest of the suite from
/// having to care about playtime), so this fixture opts back in for itself only, using the same mechanism every
/// other test uses for non-default cvars: set it on the server and take a <c>Dirty</c> pair.
/// The pool reverts cvars modified during a test when the pair is recycled
/// (<c>Robust.UnitTesting.Pool.TestPair.RevertModifiedCvars</c>), so this does not leak into other tests.
/// </remarks>
[TestFixture]
public sealed class JobGatingTest
{
    // Plain strings, not ProtoId<JobPrototype>: these only exist in [TestPrototypes], and the YAML linter
    // validates every ProtoId field against the real prototype set.
    private const string Ungated = "JobGatingUngated";
    private const string TimeGated = "JobGatingTimeGated";
    private const string Whitelisted = "JobGatingWhitelisted";

    /// <summary>
    /// A real, shipped job that has no <c>requirements</c> at all
    /// (<c>Resources/Prototypes/Roles/Jobs/Civilian/assistant.yml</c>). Used as the canary in
    /// <see cref="RoleTimersDisallowedJobsPolarity"/>: it must never be in the disallowed set.
    /// </summary>
    private const string RealUngatedJob = "Passenger";

    [TestPrototypes]
    private const string Prototypes = @"
# SharedJobSystem.SetupTrackerLookup keys a dictionary by playTimeTracker, so every job needs its own.
- type: playTimeTracker
  id: PlayTimeJobGatingUngated

- type: playTimeTracker
  id: PlayTimeJobGatingTimeGated

- type: playTimeTracker
  id: PlayTimeJobGatingWhitelisted

- type: job
  id: JobGatingUngated
  playTimeTracker: PlayTimeJobGatingUngated

- type: job
  id: JobGatingTimeGated
  playTimeTracker: PlayTimeJobGatingTimeGated
  requirements:
  - !type:OverallPlaytimeRequirement
    time: 10h

- type: job
  id: JobGatingWhitelisted
  playTimeTracker: PlayTimeJobGatingWhitelisted
  whitelisted: true
";

    /// <summary>
    /// A player with zero playtime must be refused a job that has a playtime requirement, and allowed a job that
    /// has none. Checked both on <see cref="PlayTimeTrackingSystem.IsAllowed(ICommonSession, ProtoId{JobPrototype})"/>
    /// and on <see cref="IsRoleAllowedEvent"/>, which is the event the game ticker and the ghost role system
    /// actually use to make the decision.
    /// </summary>
    [Test]
    public async Task RoleTimersGateJobsWithRequirements()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        server.CfgMan.SetCVar(CCVars.GameRoleTimers, true);
        await pair.RunTicksSync(5);

        var playTime = server.System<PlayTimeTrackingSystem>();
        var session = server.PlayerMan.SessionsDict[pair.Client.User!.Value];

        // The gating checks read DB-backed per-user data, so make sure it has finished loading.
        var userDb = server.ResolveDependency<UserDbDataManager>();
        await PoolManager.WaitUntil(server, () => userDb.IsLoadComplete(session));

        await server.WaitAssertion(() =>
        {
            // Sanity check: the fresh test player really is short of the requirement. If playtimes were never
            // loaded, IsAllowed logs an error and falls back to an empty dictionary, which would make the
            // assertion below pass for the wrong reason.
            Assert.That(server.ResolveDependency<PlayTimeTrackingManager>()
                    .TryGetTrackerTimes(session, out var times),
                Is.True,
                "Playtimes were never loaded for the test player, the gating check below would be meaningless.");
            Assert.That(times!.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall),
                Is.LessThan(TimeSpan.FromHours(10)));

            Assert.Multiple(() =>
            {
                Assert.That(playTime.IsAllowed(session, new ProtoId<JobPrototype>(Ungated)),
                    Is.True,
                    $"{Ungated} has no requirements but was refused.");

                Assert.That(playTime.IsAllowed(session, new ProtoId<JobPrototype>(TimeGated)),
                    Is.False,
                    $"{TimeGated} requires 10h of overall playtime but a player with none was allowed it. " +
                    "Role timer gating is not being applied.");
            });

            // Same decision, via the event the real spawn path raises.
            var allowed = new IsRoleAllowedEvent(session, new List<ProtoId<JobPrototype>> { Ungated }, null);
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref allowed);
            Assert.That(allowed.Cancelled, Is.False, $"IsRoleAllowedEvent cancelled for un-gated job {Ungated}.");

            var refused = new IsRoleAllowedEvent(session, new List<ProtoId<JobPrototype>> { TimeGated }, null);
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref refused);
            Assert.That(refused.Cancelled, Is.True, $"IsRoleAllowedEvent allowed time-gated job {TimeGated}.");

            // Control: with role timers back off, the same job must be allowed again. This is what makes the
            // assertions above meaningful - it proves the refusal came from the gating logic and nothing else.
            server.CfgMan.SetCVar(CCVars.GameRoleTimers, false);
            Assert.That(playTime.IsAllowed(session, new ProtoId<JobPrototype>(TimeGated)),
                Is.True,
                "game.role_timers is off, so no job should be gated on playtime.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// <see cref="PlayTimeTrackingSystem.GetDisallowedJobs"/> returns the set of jobs the player may <em>not</em>
    /// take, so a job belongs in it exactly when its requirements are <em>not</em> met.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a regression test for a polarity inversion: the loop used to add a job to
    /// the "disallowed" set when <c>JobRequirements.TryRequirementsMet</c> returned <c>true</c>, but that method
    /// returns <c>true</c> when the requirements <em>are</em> met. The set was therefore every job the player
    /// had unlocked, which <c>GameTicker.SpawnPlayer</c> then fed to
    /// <c>StationJobsSystem.PickBestAvailableJobWithPriority</c> as <c>restrictedRoles</c>.
    /// </para>
    /// <para>
    /// <see cref="RoleTimersGateJobsWithRequirements"/> does not catch this: it only exercises
    /// <see cref="PlayTimeTrackingSystem.IsAllowed(ICommonSession, ProtoId{JobPrototype})"/> and
    /// <see cref="IsRoleAllowedEvent"/>, whose polarity was never wrong.
    /// </para>
    /// </remarks>
    [Test]
    public async Task RoleTimersDisallowedJobsPolarity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        server.CfgMan.SetCVar(CCVars.GameRoleTimers, true);
        await pair.RunTicksSync(5);

        var playTime = server.System<PlayTimeTrackingSystem>();
        var session = server.PlayerMan.SessionsDict[pair.Client.User!.Value];

        // GetDisallowedJobs reads DB-backed playtimes and preferences; both managers log an ERROR (which the
        // harness turns into a failure) if asked before the load finished.
        var userDb = server.ResolveDependency<UserDbDataManager>();
        await PoolManager.WaitUntil(server, () => userDb.IsLoadComplete(session));

        var ungated = new ProtoId<JobPrototype>(Ungated);
        var timeGated = new ProtoId<JobPrototype>(TimeGated);
        var realUngated = new ProtoId<JobPrototype>(RealUngatedJob);

        await server.WaitAssertion(() =>
        {
            // Same sanity check as above: if playtimes never loaded, everything below would pass or fail for
            // the wrong reason.
            Assert.That(server.ResolveDependency<PlayTimeTrackingManager>()
                    .TryGetTrackerTimes(session, out var times),
                Is.True,
                "Playtimes were never loaded for the test player, the assertions below would be meaningless.");
            Assert.That(times!.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall),
                Is.LessThan(TimeSpan.FromHours(10)));

            var disallowed = playTime.GetDisallowedJobs(session);

            Assert.Multiple(() =>
            {
                Assert.That(disallowed.Contains(timeGated),
                    Is.True,
                    $"{TimeGated} requires 10h of overall playtime and the test player has none, so it must be " +
                    "in the disallowed set.");

                Assert.That(disallowed.Contains(ungated),
                    Is.False,
                    $"{Ungated} has no requirements, so it must not be in the disallowed set. " +
                    "GetDisallowedJobs is returning the ALLOWED set.");

                // The assertion that most directly catches the inversion: under the old code every job the
                // player qualified for landed here, which for a fresh account is almost every job in the game.
                Assert.That(disallowed.Contains(realUngated),
                    Is.False,
                    $"{RealUngatedJob} has no requirements but was reported as disallowed. GetDisallowedJobs " +
                    "is returning the set of jobs the player IS qualified for.");
            });

            // And the same thing through the event the real spawn path raises. GameTicker.Spawning.cs builds an
            // empty HashSet, raises the by-ref event over it, and hands the result to
            // PickBestAvailableJobWithPriority as restrictedRoles.
            var restrictedRoles = new HashSet<ProtoId<JobPrototype>>();
            var ev = new GetDisallowedJobsEvent(session, restrictedRoles);
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);

            Assert.Multiple(() =>
            {
                Assert.That(restrictedRoles.Contains(timeGated),
                    Is.True,
                    $"GetDisallowedJobsEvent did not restrict {TimeGated}, so the spawner could hand a " +
                    "zero-playtime player a 10h-gated job.");

                Assert.That(restrictedRoles.Contains(ungated),
                    Is.False,
                    $"GetDisallowedJobsEvent restricted {Ungated}, which has no requirements.");

                Assert.That(restrictedRoles.Contains(realUngated),
                    Is.False,
                    $"GetDisallowedJobsEvent restricted {RealUngatedJob}, which has no requirements.");
            });

            // Control: with role timers off, GetDisallowedJobs restricts nothing at all. This proves the
            // membership above came from the gating logic rather than from something else.
            server.CfgMan.SetCVar(CCVars.GameRoleTimers, false);
            Assert.That(playTime.GetDisallowedJobs(session),
                Is.Empty,
                "game.role_timers is off, so no job should be disallowed on playtime grounds.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A player who is not on the whitelist must be refused a whitelisted job while <c>game.role_whitelist</c>
    /// is on, and allowed it while it is off.
    /// </summary>
    [Test]
    public async Task JobWhitelistGatesWhitelistedJobs()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });

        var server = pair.Server;
        server.CfgMan.SetCVar(CCVars.GameRoleWhitelist, true);
        await pair.RunTicksSync(5);

        var whitelist = server.ResolveDependency<JobWhitelistManager>();
        var session = server.PlayerMan.SessionsDict[pair.Client.User!.Value];

        // The whitelist is DB-backed, and JobWhitelistManager logs an error (which fails the test) if it is
        // asked about a player whose whitelist has not loaded yet.
        var userDb = server.ResolveDependency<UserDbDataManager>();
        await PoolManager.WaitUntil(server, () => userDb.IsLoadComplete(session));

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(whitelist.IsAllowed(session, Ungated),
                    Is.True,
                    $"{Ungated} is not a whitelisted job but was refused.");

                Assert.That(whitelist.IsAllowed(session, Whitelisted),
                    Is.False,
                    $"{Whitelisted} is whitelisted and the test player is not on the whitelist, but it was allowed. " +
                    "Job whitelist gating is not being applied.");
            });

            var refused = new IsRoleAllowedEvent(session, new List<ProtoId<JobPrototype>> { Whitelisted }, null);
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref refused);
            Assert.That(refused.Cancelled, Is.True, $"IsRoleAllowedEvent allowed whitelisted job {Whitelisted}.");

            server.CfgMan.SetCVar(CCVars.GameRoleWhitelist, false);
            Assert.That(whitelist.IsAllowed(session, Whitelisted),
                Is.True,
                "game.role_whitelist is off, so no job should be gated on the whitelist.");
        });

        await pair.CleanReturnAsync();
    }
}

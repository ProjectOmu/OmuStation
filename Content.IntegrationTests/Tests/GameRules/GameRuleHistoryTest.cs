// SPDX-License-Identifier: AGPL-3.0-or-later
// Omu - added by Omu Station: regression tests for game-rule history being keyed by rule entity, and for GhostsVisible() agreeing between client and server.

using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking;
using Robust.Shared.GameObjects;
using ClientGhostVisibilitySystem = Content.Client._Shitcode.Wizard.Systems.GhostVisibilitySystem;
using ServerGhostVisibilitySystem = Content.Server._Goobstation.Wizard.Systems.GhostVisibilitySystem;

namespace Content.IntegrationTests.Tests.GameRules;

/// <summary>
///     Tests for <see cref="GameTicker.AllPreviousGameRules"/> bookkeeping, and for the shared
///     "is this rule active" query used by ghost visibility.
/// </summary>
[TestFixture]
[TestOf(typeof(GameTicker))]
public sealed class GameRuleHistoryTest
{
    /// <summary>
    ///     An inert game rule: starting it has no gameplay side effects beyond ghost visibility,
    ///     which is exactly what <see cref="GhostsVisibleAgreesBetweenClientAndServer"/> wants.
    /// </summary>
    private const string RuleId = "GhostsVisible";

    private const string Pending = RuleId + " (Pending)";

    /// <summary>
    ///     The pool default is <c>secret</c>, which picks a random sub-preset
    ///     per round and brings whatever antag rules and round-end conditions come with it. This test
    ///     asserts on rule history, so an unpinned preset makes both the runtime and the contents of
    ///     that history vary run to run - in the CI job with the tighter time budget. Every other
    ///     round-starting test in this repo pins a preset for the same reason.
    /// </summary>
    private const string PinnedPreset = "Greenshift";

    /// <summary>
    ///     Adding the same rule id twice leaves two distinguishable pending entries; starting one of them
    ///     must retire *that* entry, not an arbitrary one that happens to share the same id.
    /// </summary>
    /// <remarks>
    ///     The history used to be keyed by the string <c>id + " (Pending)"</c>, so the start-site did
    ///     <c>FindIndex(rule => rule.Item2 == id + " (Pending)")</c> and retired whichever pending entry came
    ///     first - i.e. the wrong rule instance, carrying the wrong add-time. That is what the time assertion
    ///     at the end of this test catches.
    /// </remarks>
    [Test]
    public async Task PendingRuleHistoryIsKeyedByRuleEntity()
    {
        // Needs an actual round: in the lobby every history entry is stamped TimeSpan.Zero, so the two
        // pending entries would be indistinguishable by time as well as by id.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            InLobby = true,
            Dirty = true
        });
        var server = pair.Server;
        var ticker = server.System<GameTicker>();

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
            ticker.SetGamePreset(PinnedPreset);
            ticker.StartRound();
        });

        await pair.RunTicksSync(5);

        var first = EntityUid.Invalid;
        var second = EntityUid.Invalid;
        var firstTime = TimeSpan.Zero;
        var secondTime = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            first = ticker.AddGameRule(RuleId);

            var pending = PendingTimes(ticker);
            Assert.That(pending, Has.Count.EqualTo(1), "the first add should produce exactly one pending entry");
            firstTime = pending[0];
        });

        // Let the round clock move so the two pending entries get different timestamps.
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            second = ticker.AddGameRule(RuleId);
            Assert.That(second, Is.Not.EqualTo(first));

            var pending = PendingTimes(ticker);
            Assert.That(pending, Has.Count.EqualTo(2), "the second add should produce a second, separate pending entry");
            secondTime = pending[1];

            // Sanity check: the fixture is only meaningful if the two entries are actually distinguishable.
            Assert.That(secondTime, Is.GreaterThan(firstTime));

            Assert.That(StartedTimes(ticker), Is.Empty, "neither rule has been started yet");

            Assert.That(ticker.StartGameRule(second), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            var pending = PendingTimes(ticker);
            var started = StartedTimes(ticker);

            // Exactly one pending entry and one started entry - not zero, not two.
            Assert.That(pending, Has.Count.EqualTo(1), "starting one of two pending rules must leave exactly one pending entry");
            Assert.That(started, Has.Count.EqualTo(1), "starting one of two pending rules must produce exactly one started entry");

            // The entry left pending must belong to `first`, the rule that was never started.
            // Keyed by string, the old code retired `first`'s entry and left `second`'s behind.
            Assert.That(pending[0],
                Is.EqualTo(firstTime),
                "the surviving pending entry must be the one for the rule that was never started");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     <c>GhostsVisible()</c> lives in Shared, so it must return the same answer on both sides.
    ///     It used to query the server-only <c>GameRuleComponent</c>, which can never match on the client.
    /// </summary>
    [Test]
    public async Task GhostsVisibleAgreesBetweenClientAndServer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true
        });
        var server = pair.Server;
        var client = pair.Client;

        var ticker = server.System<GameTicker>();
        var sVis = server.System<ServerGhostVisibilitySystem>();
        var cVis = client.System<ClientGhostVisibilitySystem>();

        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(sVis.GhostsVisible(), Is.False);
            Assert.That(cVis.GhostsVisible(), Is.False);
        });

        var rule = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.StartGameRule(RuleId, out rule), Is.True);
        });

        await pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(sVis.GhostsVisible(), Is.True, "server should see the active GhostsVisible rule");
            Assert.That(cVis.GhostsVisible(),
                Is.True,
                "client disagrees with the server about GhostsVisible(); the shared query is matching on server-only state");
        });

        await server.WaitPost(() => ticker.EndGameRule(rule));

        await pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(sVis.GhostsVisible(), Is.False);
            Assert.That(cVis.GhostsVisible(), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static List<TimeSpan> PendingTimes(GameTicker ticker)
    {
        return ticker.AllPreviousGameRules.Where(rule => rule.Item2 == Pending).Select(rule => rule.Item1).ToList();
    }

    private static List<TimeSpan> StartedTimes(GameTicker ticker)
    {
        return ticker.AllPreviousGameRules.Where(rule => rule.Item2 == RuleId).Select(rule => rule.Item1).ToList();
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Omu;

/// <summary>
///     Proves that restarting a round does not leak round-scoped entities into the next round.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="GameTicker.RestartRound"/> raises <see cref="RoundRestartCleanupEvent"/>, which has
///     roughly three dozen subscribers across server, shared and the fork assemblies. Nothing else in
///     this suite asserts that the contract actually holds: <c>RestartRoundTest</c> only asserts that
///     nothing logged an error, and the pool's recycle path restarts the round without inspecting the
///     result. A subscriber that resurrects, re-creates or fails to release a round-scoped entity is
///     therefore invisible today.
///     </para>
///     <para>
///     The test runs two identical round cycles back to back and compares a census taken at the same
///     point of each cycle. Anything that survives the restart shows up as growth from cycle one to
///     cycle two.
///     </para>
/// </remarks>
[TestFixture]
[TestOf(typeof(GameTicker))]
public sealed class RoundRestartParityTest
{
    /// <summary>
    ///     The preset both cycles run under.
    /// </summary>
    /// <remarks>
    ///     The pool default is <c>secret</c>, which picks a random sub-preset per round. That would
    ///     give the two cycles different rule sets and make every measure below noisy. Greenshift is
    ///     pinned instead because it is the cheapest preset that still produces a real station and a
    ///     real player spawn: two rules, no antag selection, no extra maps.
    /// </remarks>
    private const string PinnedPreset = "Greenshift";

    /// <summary>
    ///     Round-scoped measures that must not grow across a restart. Each field is justified at its
    ///     capture site in <see cref="TakeCensus"/>.
    /// </summary>
    private readonly record struct RoundCensus(
        int GameRules,
        int ActiveGameRules,
        int Stations,
        int Minds,
        HashSet<EntityUid> GameRuleEntities);

    [Test]
    public async Task RoundScopedEntitiesDoNotSurviveARestart()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var ticker = server.System<GameTicker>();

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        // Pin the preset for the whole test. No resetDelay: the preset has to survive the restart
        // between the two cycles, otherwise cycle two runs under a different rule set. It is put
        // back to the server default at the end of the test.
        await server.WaitPost(() => ticker.SetGamePreset(PinnedPreset));

        var first = await RunCycle(pair);

        // Guard against a vacuous pass: if the round never really started, every measure would be
        // zero and "did not grow" would be trivially true.
        Assert.Multiple(() =>
        {
            Assert.That(first.GameRules, Is.GreaterThan(0),
                $"Preset '{PinnedPreset}' started no game rules, so the parity check below is vacuous.");
            Assert.That(first.Stations, Is.GreaterThan(0),
                "No station was created, so the parity check below is vacuous.");
            Assert.That(first.Minds, Is.GreaterThan(0),
                "No player mind was created, so the parity check below is vacuous.");
        });

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(5);
        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        var second = await RunCycle(pair);

        // Same vacuity guard as above, for the second cycle: a round that silently failed to build a
        // station or spawn anyone would trivially satisfy "did not grow".
        Assert.Multiple(() =>
        {
            Assert.That(second.GameRules, Is.GreaterThan(0),
                "The second round started no game rules, so the parity check below is vacuous.");
            Assert.That(second.Stations, Is.GreaterThan(0),
                "The second round created no station, so the parity check below is vacuous.");
            Assert.That(second.Minds, Is.GreaterThan(0),
                "The second round created no player mind, so the parity check below is vacuous.");
        });

        Assert.Multiple(() =>
        {
            // Rule entities are created by AddGamePresetRules once per round and are never deleted
            // by EndGameRule (it only tags them). They are supposed to die with the rest of the
            // world in ResettingCleanup. A survivor here is how a gamerule ends up running twice in
            // the following round.
            //
            // Asserted by IDENTITY, not by count. Comparing the two rounds' rule counts looked like
            // the same check but was not: the count is a snapshot taken a fixed number of ticks after
            // StartRound, and a preset whose rules are not all added in that window can legitimately
            // census one short. The comparison then fails on a round that leaked nothing, and - worse
            // - would pass on a round that leaked one rule while adding one fewer. Comparing the
            // actual entities answers the question that was being asked.
            var survivors = second.GameRuleEntities.Intersect(first.GameRuleEntities).ToList();

            Assert.That(survivors, Is.Empty,
                $"{survivors.Count} game rule entities from the first round are still alive in the " +
                "second. A RoundRestartCleanupEvent subscriber is holding them, or ResettingCleanup " +
                "is not reaching them.");

            // A *running* leftover is worse than a dormant one: it keeps ticking into the next round.
            // No identity comparison is needed - the set above is a superset, so if it is empty no
            // active leftover can exist either. This stays as a cheap direct statement of the worse
            // case, bounded the same way.
            Assert.That(second.ActiveGameRules, Is.LessThanOrEqualTo(second.GameRules),
                "More active game rules than game rules, which is impossible unless the census raced.");

            // Exactly one station per round on the test map. A surviving station means the previous
            // round's station entity outlived FlushEntities, which also strands its grids and jobs.
            Assert.That(second.Stations, Is.LessThanOrEqualTo(first.Stations),
                "Station entities survived the round restart.");

            // One mind per spawned player. Minds live in nullspace, so they are exactly the kind of
            // entity that survives a map teardown. Leaked minds are directly observable: the round
            // end scoreboard lists one row per mind, so last round's players would reappear on the
            // next round's summary.
            Assert.That(second.Minds, Is.LessThanOrEqualTo(first.Minds),
                "Player minds survived the round restart. They will also show up on the next " +
                "round's end-of-round scoreboard.");
        });

        // Restore the pooled server: put the preset back to whatever the server default is (this is
        // what GameTicker.InitializeGamePreset does) and return to the lobby.
        await server.WaitPost(() =>
        {
            ticker.SetGamePreset(server.CfgMan.GetCVar(CCVars.GameLobbyDefaultPreset));
            ticker.RestartRound();
        });
        await pair.RunTicksSync(5);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Readies everyone up, starts a round, and censuses it once it is live.
    /// </summary>
    private static async Task<RoundCensus> RunCycle(TestPair pair)
    {
        var ticker = pair.Server.System<GameTicker>();

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        // ResettingCleanup puts every player back to NotReadyToPlay, so this has to be redone each
        // cycle rather than hoisted out.
        ticker.ToggleReadyAll(true);
        await pair.Server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        return await TakeCensus(pair);
    }

    /// <summary>
    ///     Snapshots the round-scoped entity census.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Taken mid-round rather than in the lobby on purpose. In the lobby the numbers are genuinely
    ///     noisy: <c>GameTicker.Update</c> preloads the maps (and with them the preset's rules and the
    ///     station) <c>RoundPreloadTime</c> before the round is due to start, so whether the lobby has
    ///     a station and rules in it depends on wall-clock timing. Mid-round, at a fixed number of
    ///     ticks after <c>StartRound</c>, all four counts are determined by the pinned preset and the
    ///     player count.
    ///     </para>
    ///     <para>
    ///     Rejected as too noisy to assert on: <c>EntityManager.EntityCount</c>. Greenshift includes
    ///     <c>BasicRoundstartVariation</c>, which randomly adds and removes trash, puddles and broken
    ///     lights on the station, so the raw total legitimately differs between two identical rounds.
    ///     Asserting on it would produce a flaky test, which is worse than no test.
    ///     </para>
    ///     <para>
    ///     <see cref="IEntityManager.Count{T}"/> is a dictionary length lookup, so this is cheap
    ///     enough to call on the server thread without perturbing anything.
    ///     </para>
    /// </remarks>
    private static async Task<RoundCensus> TakeCensus(TestPair pair)
    {
        var census = default(RoundCensus);

        await pair.Server.WaitPost(() =>
        {
            var entMan = pair.Server.EntMan;
            var ruleEntities = new HashSet<EntityUid>();
            var rules = entMan.EntityQueryEnumerator<GameRuleComponent>();
            while (rules.MoveNext(out var rule, out _))
            {
                ruleEntities.Add(rule);
            }

            census = new RoundCensus(
                entMan.Count<GameRuleComponent>(),
                entMan.Count<ActiveGameRuleComponent>(),
                entMan.Count<StationDataComponent>(),
                entMan.Count<MindComponent>(),
                ruleEntities);
        });

        return census;
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.RoundEnd;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests
{
    /// <summary>
    /// Proves that auto-end game rules funnel through <see cref="RoundEndSystem.EndRound"/> instead of
    /// calling <see cref="GameTicker.EndRound"/> directly and scheduling their own restart timer.
    ///
    /// Two properties are checked:
    /// 1. <see cref="RoundEndSystem"/> countdown state (LastCountdownStart / ExpectedCountdownEnd) is
    ///    cleared when the rule's timer fires. The old bypass path left it stale into the next round,
    ///    because RoundEndSystem was never involved in the round end at all.
    /// 2. The round restarts exactly once, i.e. only one scheduler owns the restart timer.
    /// </summary>
    [TestFixture]
    [TestOf(typeof(RoundEndSystem))]
    public sealed class RoundEndRestartFunnelTest
    {
        /// <summary>
        /// Counts how many times <see cref="GameTicker.RestartRound"/> actually ran.
        /// </summary>
        private sealed class RoundRestartCounterSystem : EntitySystem
        {
            public int RestartCount;

            public override void Initialize()
            {
                base.Initialize();
                SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => RestartCount += 1);
            }
        }

        [Test]
        public async Task Test()
        {
            await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
            var server = pair.Server;

            var entityManager = server.ResolveDependency<IEntityManager>();
            var sysManager = server.ResolveDependency<IEntitySystemManager>();
            var timing = server.ResolveDependency<IGameTiming>();
            var ticker = sysManager.GetEntitySystem<GameTicker>();
            var roundEnd = sysManager.GetEntitySystem<RoundEndSystem>();
            var counter = server.System<RoundRestartCounterSystem>();

            // Keep this tight: the suite has a hard 30 minute budget, so sub-second delays only.
            var maxRoundTime = TimeSpan.FromMilliseconds(500);
            var roundEndDelay = TimeSpan.FromMilliseconds(500);

            await server.WaitPost(() =>
            {
                ticker.StartGameRule("MaxTimeRestart", out var ruleEntity);
                Assert.That(entityManager.TryGetComponent<MaxTimeRestartRuleComponent>(ruleEntity, out var maxTime));

                maxTime.RoundMaxTime = maxRoundTime;
                maxTime.RoundEndDelay = roundEndDelay;
            });

            await server.WaitPost(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
                ticker.StartRound();
            });

            await server.WaitPost(() =>
            {
                Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

                // Dirty the RoundEndSystem's countdown state so we can tell afterwards whether
                // RoundEndSystem.EndRound actually ran. The countdown is long enough that it can
                // never expire by itself during this test.
                roundEnd.RequestRoundEnd(TimeSpan.FromMinutes(10), null, false);
            });

            // Precondition. Without this the "is null" assertions below could pass for the wrong reason.
            await server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(roundEnd.ExpectedCountdownEnd, Is.Not.Null,
                        "Shuttle was called but RoundEndSystem recorded no countdown end; the rest of this test would be meaningless.");
                    Assert.That(roundEnd.LastCountdownStart, Is.Not.Null,
                        "Shuttle was called but RoundEndSystem recorded no countdown start; the rest of this test would be meaningless.");
                });
            });

            counter.RestartCount = 0;

            // Let the MaxTimeRestart timer fire, and stop as soon as it does so that we observe
            // PostRound before the restart delay can elapse.
            var maxTicks = (int) Math.Ceiling(timing.TickRate * (maxRoundTime.TotalSeconds + 1));
            var roundEnded = false;
            for (var i = 0; i < maxTicks; i++)
            {
                await pair.RunTicksSync(1);
                if (ticker.RunLevel == GameRunLevel.PostRound)
                {
                    roundEnded = true;
                    break;
                }
            }

            Assert.That(roundEnded, Is.True, "MaxTimeRestart rule timer did not end the round.");

            await server.WaitAssertion(() =>
            {
                // The fingerprint of the bug: the rule used to call GameTicker.EndRound directly, which
                // left this state stale into the next round. Funnelling through RoundEndSystem clears it.
                Assert.Multiple(() =>
                {
                    Assert.That(roundEnd.ExpectedCountdownEnd, Is.Null,
                        "Round was ended by MaxTimeRestart but RoundEndSystem.ExpectedCountdownEnd is stale - the rule bypassed RoundEndSystem.EndRound.");
                    Assert.That(roundEnd.LastCountdownStart, Is.Null,
                        "Round was ended by MaxTimeRestart but RoundEndSystem.LastCountdownStart is stale - the rule bypassed RoundEndSystem.EndRound.");
                    Assert.That(counter.RestartCount, Is.Zero,
                        "Round restarted before the round end delay elapsed.");
                });
            });

            // Run long enough to cover TWO round end delays: if both the rule and RoundEndSystem had
            // scheduled a restart, the second one would land inside this window.
            var restartTicks = (int) Math.Ceiling(timing.TickRate * roundEndDelay.TotalSeconds * 2 * 1.5) + 20;
            await pair.RunTicksSync(restartTicks);

            await server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby),
                        "Round never restarted after the MaxTimeRestart round end delay.");
                    Assert.That(counter.RestartCount, Is.EqualTo(1),
                        "The round must be restarted by exactly one scheduler (RoundEndSystem).");
                    Assert.That(roundEnd.ExpectedCountdownEnd, Is.Null);
                    Assert.That(roundEnd.LastCountdownStart, Is.Null);
                });
            });

            await pair.CleanReturnAsync();
        }
    }
}

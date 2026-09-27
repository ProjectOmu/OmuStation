// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Omu;

/// <summary>
///     Asserts that ending a round produces a well formed round end summary, for more than one preset.
/// </summary>
/// <remarks>
///     <para>
///     Nothing in this suite previously asserted anything about the round end summary.
///     <c>RoundEndTest</c> only drives the shuttle call countdown, and stops at the run level change;
///     no test looks at <see cref="RoundEndMessageEvent"/> at all. That makes
///     <c>GameTicker.ShowRoundEndScoreboard</c> — which fans out to every
///     <c>RoundEndTextAppendEvent</c> subscriber and builds the per-player scoreboard — completely
///     unguarded.
///     </para>
///     <para>
///     This test deliberately asserts only the <b>observable contract</b>: the single
///     <see cref="RoundEndMessageEvent"/> that gets raised and networked, and the shape of its
///     payload. It does not reach into how <c>ShowRoundEndScoreboard</c> builds that payload, so it
///     stays valid across refactors of that method (for instance moving the inlined fork-specific
///     blocks out into fork-owned subscribers) and fails if such a refactor changes what clients
///     actually receive.
///     </para>
/// </remarks>
[TestFixture]
[TestOf(typeof(GameTicker))]
public sealed class RoundEndSummaryTest
{
    /// <summary>
    ///     Captures the round end message on the server.
    /// </summary>
    /// <remarks>
    ///     This system is also instantiated client side (the test assembly is loaded as a shared
    ///     assembly) where it will see the networked copy of the event. Tests only ever read the
    ///     server's instance.
    /// </remarks>
    private sealed class RoundEndSummaryTestSystem : EntitySystem
    {
        public int RaiseCount;
        public RoundEndMessageEvent? Captured;

        /// <summary>
        ///     How many minds existed at the instant the summary was raised.
        /// </summary>
        /// <remarks>
        ///     The test used to re-count minds five ticks after the event and
        ///     assert exact equality, so any mind churn in that window - round-end ghosting, a mob
        ///     deletion, antag cleanup - failed it for a reason unrelated to the scoreboard. This
        ///     handler runs synchronously inside the same call that built the list, so counting here
        ///     keeps the exact-equality property, and with it the ability to catch a dropped or
        ///     duplicated row, with no window at all.
        /// </remarks>
        public int MindCountAtRaise;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        }

        /// <summary>
        ///     Whether this system is capturing at all. Off unless a test turns it on.
        /// </summary>
        /// <remarks>
        ///     The test assembly is loaded as a shared assembly, so this
        ///     system exists in every pair in the suite, not just this fixture's. Without a gate it
        ///     held onto a full RoundEndMessageEvent - and now a mind enumeration as well - for every
        ///     round end in every test, in the CI step that is explicitly memory-constrained.
        /// </remarks>
        public bool Armed;

        /// <summary>
        ///     Starts capturing, from a clean slate.
        /// </summary>
        public void Arm()
        {
            Reset();
            Armed = true;
        }

        /// <summary>
        ///     Stops capturing and drops whatever was captured. Must be called before the pair goes
        ///     back to the pool, or this fixture keeps costing every later test that reuses it.
        /// </summary>
        public void Disarm()
        {
            Armed = false;
            Reset();
        }

        public void Reset()
        {
            RaiseCount = 0;
            Captured = null;
            MindCountAtRaise = 0;
        }

        private void OnRoundEnd(RoundEndMessageEvent ev)
        {
            if (!Armed)
                return;

            RaiseCount += 1;
            Captured = ev;

            var minds = EntityQueryEnumerator<MindComponent>();
            while (minds.MoveNext(out _, out _))
            {
                MindCountAtRaise += 1;
            }
        }
    }

    /// <summary>
    ///     Ends a round under <paramref name="presetId"/> and checks the resulting summary.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Only two presets are run, not all of them. Each case costs a full round start (station map
    ///     load plus player spawns) and the suite has a hard 31 minute FailFast, so iterating over
    ///     every <see cref="GamePresetPrototype"/> is not affordable here.
    ///     </para>
    ///     <para>
    ///     <c>Greenshift</c> stands in for the plain, no-antag case: it is the cheapest preset that
    ///     still produces a real station and a real spawn. The server default preset (<c>secret</c>)
    ///     is deliberately <b>not</b> used, because it picks a random sub-preset per round — its
    ///     gamemode title and player roles would differ run to run and the assertions below would be
    ///     flaky.
    ///     </para>
    ///     <para>
    ///     <c>Traitor</c> is the antag case. It also exercises the multi-player scoreboard, since it
    ///     needs enough ready players to not cancel preset selection.
    ///     </para>
    /// </remarks>
    [Test]
    [TestCase("Greenshift")]
    [TestCase("Traitor")]
    public async Task RoundEndSummaryIsWellFormed(string presetId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var sys = server.System<RoundEndSummaryTestSystem>();

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        GamePresetPrototype? preset = null;
        var requiredPlayers = 0;

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.TryFindGamePreset(presetId, out preset),
                $"Could not find game preset '{presetId}'.");

            requiredPlayers = MinReadyPlayersFor(server.ProtoMan, server.ResolveDependency<IComponentFactory>(), preset!);

            ticker.SetGamePreset(preset!);
        });

        // Not enough ready players and GameRuleSystem cancels preset selection, at which point the
        // ticker silently falls back to another preset and the gamemode title assertion below would
        // be comparing against the wrong preset. Top up with dummies rather than hardcoding a count,
        // so this keeps working when a rule's minPlayers changes in yaml.
        var dummies = Math.Max(0, requiredPlayers - 1);
        if (dummies > 0)
        {
            await server.AddDummySessions(dummies);
            await pair.RunTicksSync(5);
        }

        await server.WaitPost(() => sys.Arm());

        ticker.ToggleReadyAll(true);
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(presetId),
                $"Preset '{presetId}' did not survive round start; the ticker fell back to another " +
                "preset, so this test case is not actually testing the preset it claims to.");
            Assert.That(sys.RaiseCount, Is.Zero,
                "A round end message was raised before the round was ended.");
        });

        await server.WaitPost(() => ticker.EndRound());
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sys.RaiseCount, Is.EqualTo(1),
                "Ending the round did not raise exactly one RoundEndMessageEvent.");

            var ev = sys.Captured!;

            // ShowRoundEndScoreboard walks EntityQueryEnumerator<MindComponent> and emits one entry
            // per mind, so this is an exact equality rather than a guess: it catches both dropped
            // rows (a player missing from the summary) and duplicated rows. The count is taken by
            // the capture handler at the instant of the raise, not here - see MindCountAtRaise.
            var mindCount = sys.MindCountAtRaise;

            Assert.Multiple(() =>
            {
                Assert.That(ev.RoundEndText, Is.Not.Null.And.Not.Empty,
                    "Round end text was null or empty. RoundEndTextAppendEvent no longer contributes " +
                    "to the summary.");

                Assert.That(ev.GamemodeTitle, Is.EqualTo(Loc.GetString(preset!.ModeTitle)),
                    "The summary's gamemode title does not match the running preset's localised name.");

                Assert.That(ev.AllPlayersEndInfo, Is.Not.Null);
                Assert.That(ev.AllPlayersEndInfo, Is.Not.Empty,
                    "The round ended with spawned players but the scoreboard is empty.");
                Assert.That(ev.AllPlayersEndInfo, Has.Length.EqualTo(mindCount),
                    "The scoreboard does not have exactly one entry per mind that existed when it was built.");
                Assert.That(ev.PlayerCount, Is.EqualTo(ev.AllPlayersEndInfo.Length),
                    "The summary's player count disagrees with the number of scoreboard entries.");

                // The real connected player has to be on the scoreboard, not just some count of rows.
                Assert.That(ev.AllPlayersEndInfo.Any(i => i.PlayerGuid == pair.Client.User!.Value),
                    "The connected player is missing from the round end scoreboard.");

                Assert.That(ev.RoundId, Is.EqualTo(ticker.RoundId));
            });

            Assert.Multiple(() =>
            {
                foreach (var info in ev.AllPlayersEndInfo)
                {
                    Assert.That(info.PlayerOOCName, Is.Not.Null.And.Not.Empty,
                        "A scoreboard entry has no OOC name.");
                    Assert.That(info.Role, Is.Not.Null,
                        $"Scoreboard entry for '{info.PlayerOOCName}' has a null role. Every entry " +
                        "must fall back to a role name even when the player had no job.");
                }
            });
        });

        // Restore the pooled server: put the preset back to whatever the server default is (this is
        // what GameTicker.InitializeGamePreset does) and return to the lobby.
        await server.WaitPost(() =>
        {
            sys.Disarm();
            ticker.SetGamePreset(server.CfgMan.GetCVar(CCVars.GameLobbyDefaultPreset));
            ticker.RestartRound();
        });
        await pair.RunTicksSync(5);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The number of ready players the preset needs before <c>GameRuleSystem</c> stops cancelling
    ///     preset selection. Only walks the preset's own rule list, which is a handful of prototypes.
    /// </summary>
    private static int MinReadyPlayersFor(IPrototypeManager protoMan, IComponentFactory compFact, GamePresetPrototype preset)
    {
        var required = 0;

        foreach (var ruleId in preset.Rules)
        {
            if (!protoMan.TryIndex<EntityPrototype>(ruleId.Id, out var ruleProto))
                continue;

            if (!ruleProto.TryGetComponent<GameRuleComponent>(out var rule, compFact))
                continue;

            if (rule.CancelPresetOnTooFewPlayers)
                required = Math.Max(required, rule.MinPlayers);
        }

        return required;
    }
}

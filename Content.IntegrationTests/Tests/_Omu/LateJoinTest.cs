// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Omu;

/// <summary>
///     Covers late joining, which had no test anywhere in the repository.
/// </summary>
/// <remarks>
///     <para>
///     Every existing round test spawns its players at round start via <c>StartRound</c>. The late
///     join path — <see cref="GameTicker.MakeJoinGame"/> into <c>SpawnPlayer(lateJoin: true)</c> — was
///     never exercised, and neither was the gate that path checks.
///     </para>
///     <para>
///     The gate is worth particular attention: <c>RefreshLateJoinAllowedEvent</c> has zero subscribers
///     repo-wide, so if <c>GameTicker</c> stopped raising it nothing at all would notice until a fork
///     added a subscriber and quietly found it dead.
///     </para>
/// </remarks>
[TestFixture]
[TestOf(typeof(GameTicker))]
public sealed class LateJoinTest
{
    /// <summary>
    ///     The only job the default test map ("Empty") offers.
    /// </summary>
    private static readonly ProtoId<JobPrototype> Passenger = "Passenger";

    /// <summary>
    ///     Pinned so the round start is deterministic and cheap.
    /// </summary>
    /// <remarks>
    ///     Greenshift has no rule with a <c>minPlayers</c> floor, so the round starts cleanly with
    ///     nobody readied up — which is exactly the state a late join test needs. The pool default
    ///     (<c>secret</c>) would pick a random sub-preset that may cancel preset selection with zero
    ///     ready players and silently fall back to something else.
    /// </remarks>
    private const string PinnedPreset = "Greenshift";

    /// <summary>
    ///     Counts <see cref="RefreshLateJoinAllowedEvent"/> raises. Deliberately does not call
    ///     <c>Disallow()</c>, so it cannot change the behaviour of any other test sharing the pool.
    /// </summary>
    private sealed class LateJoinRefreshTestSystem : EntitySystem
    {
        public int RefreshCount;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RefreshLateJoinAllowedEvent>(OnRefresh);
        }

        private void OnRefresh(RefreshLateJoinAllowedEvent ev)
        {
            RefreshCount += 1;
        }
    }

    /// <summary>
    ///     A player who joins after the round has started gets a mind, a job and a body on the station.
    /// </summary>
    /// <remarks>
    ///     Fails if the late join path stops assigning a job (the player would spawn roleless), stops
    ///     creating or attaching a mind, or spawns the body off-station. Also fails if
    ///     <c>GameTicker</c> stops raising <see cref="RefreshLateJoinAllowedEvent"/> during round
    ///     start, which is currently undetectable by any other means.
    /// </remarks>
    [Test]
    public async Task LateJoinSpawnsPlayerWithMindAndJob()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var mindSys = server.System<MindSystem>();
        var jobSys = server.System<SharedJobSystem>();
        var stationSys = server.System<StationSystem>();
        var refreshSys = server.System<LateJoinRefreshTestSystem>();

        var user = pair.Client.User!.Value;

        await StartRoundWithNobodyReady(pair, ticker, refreshSys);

        Assert.Multiple(() =>
        {
            // SpawnPlayers raises RefreshLateJoinAllowedEvent unconditionally on every round start.
            Assert.That(refreshSys.RefreshCount, Is.GreaterThanOrEqualTo(1),
                "GameTicker did not raise RefreshLateJoinAllowedEvent during round start. The event " +
                "has no subscribers in-tree, so nothing else would catch this.");
            Assert.That(ticker.DisallowLateJoin, Is.False,
                "Late joins were disallowed even though nothing asked for that.");

            // Nobody readied up, so the player is still sitting in the lobby: any spawn from here on
            // is genuinely a late join and not a round-start spawn.
            Assert.That(ticker.PlayerGameStatuses[user], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));
            Assert.That(pair.Player?.AttachedEntity, Is.Null);
        });

        var station = EntityUid.Invalid;
        await server.WaitAssertion(() =>
        {
            station = stationSys.GetStations().Single();
            ticker.MakeJoinGame(pair.Player!, station);
        });
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.PlayerGameStatuses[user], Is.EqualTo(PlayerGameStatus.JoinedGame),
                "The late joining player was never marked as having joined the game.");

            var uid = pair.Player?.AttachedEntity;
            Assert.That(server.EntMan.EntityExists(uid),
                "The late joining player was not attached to a spawned entity.");

            var mind = mindSys.GetMind(uid!.Value);
            Assert.That(server.EntMan.EntityExists(mind),
                "The late joining player's body has no mind.");

            Assert.Multiple(() =>
            {
                Assert.That(jobSys.MindTryGetJobId(mind, out var job),
                    "The late joining player's mind was given no job role.");
                Assert.That(job, Is.EqualTo(Passenger));

                Assert.That(stationSys.GetOwningStation(uid.Value), Is.EqualTo(station),
                    "The late joining player was not spawned on the station.");

                Assert.That(server.EntMan.HasComponent<GhostComponent>(uid.Value), Is.False,
                    "The late joining player was ghosted instead of spawned as crew.");
            });
        });

        await Restore(pair, ticker);
        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     With late joins disallowed, a would-be late joiner is refused a body and gets an observer
    ///     ghost instead.
    /// </summary>
    /// <remarks>
    ///     Fails if <c>SpawnPlayer</c> stops honouring <see cref="GameTicker.DisallowLateJoin"/>,
    ///     which would let players walk into rounds that have declared themselves closed (nukies
    ///     mid-detonation, wizard rounds, admin-locked rounds).
    /// </remarks>
    [Test]
    public async Task DisallowLateJoinRefusesSpawn()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var mindSys = server.System<MindSystem>();
        var jobSys = server.System<SharedJobSystem>();
        var stationSys = server.System<StationSystem>();
        var refreshSys = server.System<LateJoinRefreshTestSystem>();

        var user = pair.Client.User!.Value;

        await StartRoundWithNobodyReady(pair, ticker, refreshSys);

        // Set after the round starts, so this asserts on a refresh that has actually run. The CVar
        // is a floor rather than a value the refresh overwrites - RefreshLateJoinAllowed ORs
        // CCVars.GameDisallowLateJoins with the event's result - so a value set before round start
        // would survive too; it just would not prove that a refresh honours it.
        await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.GameDisallowLateJoins, true));
        await pair.RunTicksSync(1);

        Assert.That(ticker.DisallowLateJoin, Is.True,
            "Setting game.disallowlatejoins did not disallow late joins.");

        await server.WaitPost(() =>
        {
            var station = stationSys.GetStations().Single();
            ticker.MakeJoinGame(pair.Player!, station);
        });
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var uid = pair.Player?.AttachedEntity;
            Assert.That(server.EntMan.EntityExists(uid),
                "The refused late joiner was left with no entity at all; they should get an observer ghost.");

            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<GhostComponent>(uid!.Value),
                    "Late joins were disallowed but the player was still spawned as a crew member.");

                var mind = mindSys.GetMind(uid.Value);
                Assert.That(server.EntMan.EntityExists(mind),
                    "The refused late joiner's observer ghost has no mind.");
                Assert.That(jobSys.MindTryGetJobId(mind, out _), Is.False,
                    "Late joins were disallowed but the player was still assigned a station job.");
            });

            Assert.That(ticker.PlayerGameStatuses[user], Is.EqualTo(PlayerGameStatus.JoinedGame));
        });

        // The cvar is reverted automatically when the pair is returned.
        await Restore(pair, ticker);
        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Starts a round under <see cref="PinnedPreset"/> with nobody readied up, so the connected
    ///     player is left in the lobby and available to late join.
    /// </summary>
    private static async Task StartRoundWithNobodyReady(
        TestPair pair,
        GameTicker ticker,
        LateJoinRefreshTestSystem refreshSys)
    {
        var server = pair.Server;

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        await server.WaitPost(() =>
        {
            ticker.SetGamePreset(PinnedPreset);
            refreshSys.RefreshCount = 0;
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        Assert.That(ticker.CurrentPreset?.ID, Is.EqualTo(PinnedPreset),
            $"Preset '{PinnedPreset}' did not survive round start; the ticker fell back to another preset.");
    }

    /// <summary>
    ///     Puts the pooled server back the way it was found: server default preset, back in the lobby.
    /// </summary>
    private static async Task Restore(TestPair pair, GameTicker ticker)
    {
        await pair.Server.WaitPost(() =>
        {
            ticker.SetGamePreset(pair.Server.CfgMan.GetCVar(CCVars.GameLobbyDefaultPreset));
            ticker.RestartRound();
        });
        await pair.RunTicksSync(5);
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Omu.Server.Spawning;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Omu;

/// <summary>
///     Covers the serverside trait gate: the check that a character's selected traits are ones the player is
///     actually allowed to have.
/// </summary>
/// <remarks>
///     <para>
///     The trait picker is clientside, so a modified client can select a trait the UI greys out. The gate is the
///     only thing standing between that and the trait being applied on spawn. It used to be inlined in
///     <c>GameTicker.SpawnPlayer</c> and had no test at all; it now lives in
///     <see cref="TraitRestrictionSpawnSystem"/> and hangs off <see cref="IsSpawnAllowedEvent"/>.
///     </para>
///     <para>
///     The shape of the refusal matters as much as the refusal itself. A refused player must be left <i>in the
///     lobby</i> - not joined to the round, not ghosted, not spawned. That is why the gate cannot be a
///     <see cref="PlayerBeforeSpawnEvent"/> subscriber: handling that event makes the ticker call
///     <c>PlayerJoinGame</c>, which would pull the refused player out of the lobby with no body to control.
///     <see cref="TraitRefusalLeavesPlayerInLobby"/> asserts the lobby state explicitly, so a future move back
///     onto that hook fails here instead of shipping.
///     </para>
/// </remarks>
[TestFixture]
[TestOf(typeof(TraitRestrictionSpawnSystem))]
public sealed class TraitRestrictionSpawnTest
{
    /// <summary>
    ///     A trait nobody can ever take. <c>AgeRequirement</c> is deterministic - it reads only the profile, so
    ///     unlike a playtime or whitelist requirement it needs no database state and no cvar opt-in
    ///     (<c>JobRequirements.TryRequirementsMet</c> does not consult <c>game.role_timers</c>; only
    ///     <c>PlayTimeTrackingSystem.IsAllowed</c> does, and the trait gate does not go through it).
    /// </summary>
    // Plain string, not ProtoId<TraitPrototype>: [TestPrototypes] ids are not in the real prototype set
    // and the YAML linter validates every ProtoId field against it.
    private const string Unmeetable = "TraitRestrictionTestUnmeetable";

    /// <summary>
    ///     Control trait: identical, minus the requirement. Proves a refusal came from the requirement check and
    ///     not merely from having any trait selected at all.
    /// </summary>
    private const string Meetable = "TraitRestrictionTestMeetable";

    /// <summary>
    ///     Both traits are free and uncounted, so the max-traits and global-points arms of the gate cannot fire.
    ///     The only difference between them is the requirement.
    /// </summary>
    [TestPrototypes]
    private const string Prototypes = @"
# name/description must be real loc ids: PrototypeTests validates every LocId on every prototype,
# and TraitPrototype.Name defaults to an empty string, which fails that validation.
- type: trait
  id: TraitRestrictionTestMeetable
  name: trait-blindness-name
  description: trait-blindness-desc
  globalCost: 0
  countsTowardsMaxTraits: false

- type: trait
  id: TraitRestrictionTestUnmeetable
  name: trait-blindness-name
  description: trait-blindness-desc
  globalCost: 0
  countsTowardsMaxTraits: false
  requirements:
  - !type:AgeRequirement
    requiredAge: 999
";

    /// <summary>
    ///     Pinned so the round start is deterministic and cheap, and so nobody is readied up.
    /// </summary>
    private const string PinnedPreset = "Greenshift";

    /// <summary>
    ///     Counts <see cref="PlayerBeforeSpawnEvent"/> raises, so the test can prove a refused spawn never
    ///     reaches the hook that game rules such as <c>DeathMatchRuleSystem</c> spawn players from.
    /// </summary>
    /// <remarks>
    ///     Deliberately never sets <c>Handled</c>, so it cannot change behaviour for any other test sharing the
    ///     pool. The <c>Handled</c> guard is here because the event bus does not stop dispatching on
    ///     <c>Handled</c> - every subscriber has to check it, including this one.
    /// </remarks>
    private sealed class BeforeSpawnCountTestSystem : EntitySystem
    {
        public int Count;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        }

        private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
        {
            if (ev.Handled)
                return;

            Count += 1;
        }
    }

    /// <summary>
    ///     A character carrying a trait whose requirements it does not meet is refused a spawn and left in the
    ///     lobby; the same character with a trait it does meet spawns normally.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Fails if the gate stops running (the refused character would spawn with the trait applied), if it
    ///     starts refusing everyone (the control leg would fail), or if the refusal stops being lobby-safe -
    ///     which is what would happen if the gate were moved onto <see cref="PlayerBeforeSpawnEvent"/>.
    ///     </para>
    ///     <para>
    ///     Also asserts the ordering property: a refused player must never reach
    ///     <see cref="PlayerBeforeSpawnEvent"/>, because a game rule subscribed there (deathmatch, wizard) would
    ///     otherwise happily spawn the character the gate just rejected. Spinning up a real deathmatch round to
    ///     check that would cost far more than the assertion is worth, so it is asserted on the event itself.
    ///     </para>
    /// </remarks>
    [Test]
    public async Task TraitRefusalLeavesPlayerInLobby()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var traitGate = server.System<TraitRestrictionSpawnSystem>();
        var stationSys = server.System<StationSystem>();
        var beforeSpawn = server.System<BeforeSpawnCountTestSystem>();
        var prefMan = server.ResolveDependency<IServerPreferencesManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var user = pair.Client.User!.Value;

        // The refusal path forks on this. With a lobby, a refused player stays in it; without one they are
        // ghosted instead. Assert which branch is under test so this cannot quietly become the other one.
        Assert.That(server.CfgMan.GetCVar(CCVars.GameLobbyEnabled), Is.True);

        var original = (HumanoidCharacterProfile) prefMan.GetPreferences(user).Characters[0];
        Assert.That(prefMan.GetPreferences(user).SelectedCharacterIndex, Is.EqualTo(0));

        try
        {
            await StartRoundWithNobodyReady(pair, ticker);

            var session = server.PlayerMan.SessionsDict[user];

            // The policy itself, checked directly on both profiles before any spawning happens.
            await server.WaitAssertion(() =>
            {
                var bad = original.WithTraitPreference(Unmeetable, protoMan);
                var good = original.WithTraitPreference(Meetable, protoMan);

                // If EnsureValid or WithTraitPreference ever start dropping traits, the assertions below would
                // pass for the wrong reason - the profile simply would not have the trait any more.
                Assert.That(bad.TraitPreferences, Does.Contain(Unmeetable));
                Assert.That(good.TraitPreferences, Does.Contain(Meetable));

                Assert.Multiple(() =>
                {
                    Assert.That(traitGate.IsTraitSelectionAllowed(session, good),
                        Is.True,
                        $"{Meetable} has no requirements but the trait gate refused it.");

                    Assert.That(traitGate.IsTraitSelectionAllowed(session, bad),
                        Is.False,
                        $"{Unmeetable} requires an age no character can reach, but the trait gate allowed it. " +
                        "Trait requirements are not being enforced serverside.");
                });
            });

            var station = EntityUid.Invalid;
            await server.WaitPost(() => station = stationSys.GetStations().Single());

            // Leg 1: the unmeetable trait. Must be refused, and must be refused into the lobby.
            await SetTrait(pair, prefMan, protoMan, user, original, Unmeetable);

            await server.WaitPost(() =>
            {
                beforeSpawn.Count = 0;
                ticker.MakeJoinGame(pair.Player!, station);
            });
            await pair.RunTicksSync(10);

            await server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(pair.Player?.AttachedEntity, Is.Null,
                        "A character with a trait it does not meet the requirements for was still spawned.");

                    Assert.That(ticker.PlayerGameStatuses[user], Is.EqualTo(PlayerGameStatus.NotReadyToPlay),
                        "The refused player was joined to the round anyway. A trait refusal has to leave them " +
                        "in the lobby - this is exactly what routing the gate through PlayerBeforeSpawnEvent " +
                        "would break, because handling that event forces a PlayerJoinGame.");

                    Assert.That(beforeSpawn.Count, Is.Zero,
                        "PlayerBeforeSpawnEvent was raised for a trait-refused player. Any game rule " +
                        "subscribed to it (deathmatch, wizard) would have spawned them.");
                });
            });

            // Leg 2: the same character with a trait it does meet. Must spawn.
            await SetTrait(pair, prefMan, protoMan, user, original, Meetable);

            await server.WaitPost(() =>
            {
                beforeSpawn.Count = 0;
                ticker.MakeJoinGame(pair.Player!, station);
            });
            await pair.RunTicksSync(10);

            await server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(server.EntMan.EntityExists(pair.Player?.AttachedEntity),
                        "A character whose traits are all allowed was refused a spawn. The trait gate is " +
                        "refusing everyone, which would make the refusal assertions above meaningless.");

                    Assert.That(ticker.PlayerGameStatuses[user], Is.EqualTo(PlayerGameStatus.JoinedGame));

                    Assert.That(beforeSpawn.Count, Is.GreaterThanOrEqualTo(1),
                        "PlayerBeforeSpawnEvent was never raised for an allowed spawn, so the count of zero " +
                        "asserted for the refused spawn proves nothing.");
                });
            });
        }
        finally
        {
            // Preference resetting on recycle only covers profiles changed through TestPair's own helpers.
            await server.WaitPost(() => prefMan.SetProfile(user, 0, original).Wait());
            await Restore(pair, ticker);
        }

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Replaces slot 0 with <paramref name="baseline"/> plus exactly one trait.
    /// </summary>
    private static async Task SetTrait(
        TestPair pair,
        IServerPreferencesManager prefMan,
        IPrototypeManager protoMan,
        NetUserId user,
        HumanoidCharacterProfile baseline,
        ProtoId<TraitPrototype> trait)
    {
        var profile = baseline.WithTraitPreference(trait, protoMan);
        await pair.Server.WaitPost(() => prefMan.SetProfile(user, 0, profile).Wait());
        await pair.RunTicksSync(1);

        // SetProfile runs EnsureValid, which is allowed to drop traits. Make sure it did not drop this one.
        var stored = (HumanoidCharacterProfile) prefMan.GetPreferences(user).Characters[0];
        Assert.That(stored.TraitPreferences, Does.Contain(trait),
            $"Trait {trait} did not survive being stored, the spawn below would not exercise the gate.");
    }

    /// <summary>
    ///     Starts a round under <see cref="PinnedPreset"/> with nobody readied up, leaving the connected player
    ///     in the lobby and available to join.
    /// </summary>
    private static async Task StartRoundWithNobodyReady(TestPair pair, GameTicker ticker)
    {
        var server = pair.Server;

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        await server.WaitPost(() =>
        {
            ticker.SetGamePreset(PinnedPreset);
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

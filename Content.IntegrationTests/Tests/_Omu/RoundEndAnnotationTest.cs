// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Omu;

/// <summary>
///     Proves that <see cref="RoundEndPlayerInfoEvent"/> is a working extension point: annotations
///     written by subscribers in other assemblies survive into the <see cref="RoundEndMessageEvent"/>
///     that is handed to clients, and two subscribers annotating different fields do not clobber
///     each other.
/// </summary>
/// <remarks>
///     This is the regression test for the round-end structured-data hook. It must fail if
///     <c>GameTicker.ShowRoundEndScoreboard</c> stops raising the event, or if the event is ever
///     reshaped so that subscriber writes land on a copy instead of the entry that gets sent.
/// </remarks>
[TestFixture]
public sealed class RoundEndAnnotationTest
{
    private const string IcSentinel = "RoundEndAnnotationTest-IC";
    private const string RoleSentinel = "RoundEndAnnotationTest-Role";

    /// <summary>
    ///     First annotating subscriber. Also captures the outgoing round end message.
    ///     Disabled unless a test explicitly arms it, so it can never perturb other tests that
    ///     share the pooled server.
    /// </summary>
    private sealed class RoundEndAnnotationTestSystemA : EntitySystem
    {
        public EntityUid? Target;
        public int RaiseCount;
        public RoundEndMessageEvent? Captured;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundEndPlayerInfoEvent>(OnPlayerInfo);
            SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        }

        private void OnPlayerInfo(RoundEndPlayerInfoEvent args)
        {
            if (Target != args.MindId)
                return;

            RaiseCount += 1;
            args.Info.PlayerICName = IcSentinel;
        }

        private void OnRoundEnd(RoundEndMessageEvent ev)
        {
            if (Target != null)
                Captured = ev;
        }
    }

    /// <summary>
    ///     Second annotating subscriber, writing a different field of the same entry.
    /// </summary>
    private sealed class RoundEndAnnotationTestSystemB : EntitySystem
    {
        public EntityUid? Target;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundEndPlayerInfoEvent>(OnPlayerInfo);
        }

        private void OnPlayerInfo(RoundEndPlayerInfoEvent args)
        {
            if (Target != args.MindId)
                return;

            args.Info.Role = RoleSentinel;
        }
    }

    [Test]
    public async Task AnnotationsReachTheRoundEndMessage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        var entMan = server.ResolveDependency<IServerEntityManager>();
        var ticker = entMan.EntitySysManager.GetEntitySystem<GameTicker>();
        var mindSys = entMan.EntitySysManager.GetEntitySystem<SharedMindSystem>();
        var sysA = entMan.EntitySysManager.GetEntitySystem<RoundEndAnnotationTestSystemA>();
        var sysB = entMan.EntitySysManager.GetEntitySystem<RoundEndAnnotationTestSystemB>();

        EntityUid mind = default;

        await server.WaitAssertion(() =>
        {
            mind = mindSys.CreateMind(null).Owner;

            sysA.Target = mind;
            sysA.RaiseCount = 0;
            sysA.Captured = null;
            sysB.Target = mind;

            ticker.ShowRoundEndScoreboard();

            Assert.Multiple(() =>
            {
                Assert.That(sysA.RaiseCount,
                    Is.EqualTo(1),
                    "RoundEndPlayerInfoEvent was not raised exactly once for the test mind.");
                Assert.That(sysA.Captured,
                    Is.Not.Null,
                    "No RoundEndMessageEvent was raised by ShowRoundEndScoreboard.");
            });

            var entries = sysA.Captured!.AllPlayersEndInfo
                .Where(i => i.PlayerICName == IcSentinel)
                .ToArray();

            Assert.That(entries,
                Has.Length.EqualTo(1),
                "The subscriber's annotation did not survive into the round end message. " +
                "The per-player entry was copied after the event was raised.");

            // Both subscribers annotated the same entry, each writing a different field. If the
            // event ever hands subscribers a copy of the struct, one of these two writes gets lost.
            Assert.That(entries[0].Role,
                Is.EqualTo(RoleSentinel),
                "A second subscriber's annotation clobbered or was clobbered by the first.");

            // Sanity: the upstream data that GameTicker fills in before raising is still intact.
            Assert.That(entries[0].Antag, Is.False);
            Assert.That(entries[0].PlayerOOCName, Is.Not.Null);
        });

        // Don't leave the annotating subscribers armed, or the stray mind alive, for other tests.
        await server.WaitPost(() =>
        {
            sysA.Target = null;
            sysA.Captured = null;
            sysB.Target = null;

            if (entMan.EntityExists(mind))
                entMan.DeleteEntity(mind);
        });

        await pair.CleanReturnAsync();
    }
}

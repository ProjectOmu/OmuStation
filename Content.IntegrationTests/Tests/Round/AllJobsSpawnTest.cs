// SPDX-License-Identifier: AGPL-3.0-or-later
// Omu - added by Omu Station: spawns every JobPrototype with full gear, so a job whose loadout errors fails CI.

#nullable enable
using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Round;

/// <summary>
/// Sweeps every <see cref="JobPrototype"/> and spawns a character for it, so that a job with broken starting
/// gear, a broken loadout, or a broken <see cref="JobPrototype.JobEntity"/> fails CI instead of only failing
/// the first time someone picks that job in a live round.
/// </summary>
/// <remarks>
/// <para>
/// This uses <see cref="StationSpawningSystem.SpawnPlayerMob"/> rather than
/// <see cref="StationSpawningSystem.SpawnPlayerCharacterOnStation"/>. SpawnPlayerCharacterOnStation only picks a
/// spawn location (via <c>PlayerSpawningEvent</c>) and then calls SpawnPlayerMob, so all of the per-job work -
/// starting gear, role loadout, ID card, PDA, job entity - is in SpawnPlayerMob. Going through the station
/// variant would require loading a real station map per sweep, which is the single most expensive thing this
/// suite does, and would only additionally cover spawn-point selection, which is not job-specific.
/// </para>
/// <para>
/// Cost control: ONE pair and ONE test map for the whole sweep, each mob deleted immediately after it is
/// checked. Errors do not need an explicit assertion - the integration harness' log handler fails the test on
/// any log at or above <c>RTCVars.FailureLogLevel</c>.
/// </para>
/// </remarks>
[TestFixture]
public sealed class AllJobsSpawnTest
{
    /// <summary>
    /// How many jobs to spawn inside a single server tick before letting the server run again.
    /// </summary>
    private const int BatchSize = 10;

    /// <summary>
    ///     Jobs this sweep cannot currently spawn, with the reason. Keep this list as short as possible and
    ///     always record WHY - an entry here is a known bug, not a licence to ignore one.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <b>Borg</b> - the Borg job spawns through the character path with <c>speciesOverride: Cyborg</c>
    ///     (<c>Resources/Prototypes/Roles/Jobs/Science/borg.yml</c>), and the <c>Cyborg</c> species'
    ///     <c>prototype</c> is <c>PlayerBorgBattery</c>
    ///     (<c>Resources/Prototypes/_Shitmed/Species/cyborg.yml</c>), which carries no
    ///     <c>HumanoidAppearanceComponent</c>. So <c>StationSpawningSystem.SpawnPlayerMob</c> reaches
    ///     <c>SharedHumanoidAppearanceSystem.LoadProfile</c>, whose <c>Resolve</c> logs an error on every
    ///     borg spawn. This is <b>pre-existing</b> and happens on live servers today - the character path was
    ///     already taken for borgs before the job carve-outs were moved onto prototype data, so the spawn
    ///     path is unchanged. Remove this entry once borg spawning stops logging that error.
    ///     </para>
    /// </remarks>
    private static readonly string[] KnownBrokenJobs =
    {
        "Borg",
    };

    [Test]
    public async Task AllJobsSpawn()
    {
        // Dirty: spawning ~all jobs leaves the pair in no state to be fast-recycled.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var spawning = entMan.System<StationSpawningSystem>();

        var jobs = protoMan.EnumeratePrototypes<JobPrototype>()
            .Where(x => !pair.IsTestPrototype(x))
            .Where(x => !KnownBrokenJobs.Contains(x.ID))
            .OrderBy(x => x.ID, StringComparer.Ordinal)
            .Select(x => x.ID)
            .ToList();

        Assert.That(jobs, Is.Not.Empty, "No job prototypes were found, this test is not testing anything.");

        var testMap = await pair.CreateTestMap();
        var coords = testMap.GridCoords;

        foreach (var batch in jobs.Chunk(BatchSize))
        {
            await server.WaitAssertion(() =>
            {
                foreach (var job in batch)
                {
                    EntityUid mob;
                    try
                    {
                        // A fresh default profile, i.e. what a brand new player spawns with.
                        mob = spawning.SpawnPlayerMob(coords, job, new HumanoidCharacterProfile(), station: null);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to spawn job {job}", ex);
                    }

                    Assert.That(entMan.EntityExists(mob), $"Job {job} spawned an invalid entity.");

                    entMan.DeleteEntity(mob);
                }
            });

            await server.WaitRunTicks(1);
        }

        await server.WaitPost(() => entMan.DeleteEntity(testMap.MapUid));
        await pair.CleanReturnAsync();
    }
}

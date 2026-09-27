// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Omu;

/// <summary>
/// The borg spawn carve-outs in <see cref="StationSpawningSystem.SpawnPlayerMob"/> used to be keyed on the job's
/// localization id (<c>job-name-borg</c>), which meant renaming that LocId would silently change spawn behaviour.
/// They are now driven by <see cref="JobPrototype.UseCharacterSpawn"/> and <see cref="JobPrototype.SpeciesOverride"/>.
/// These tests pin that down with throwaway job prototypes, and assert the real borg job carries the fields.
/// </summary>
[TestFixture]
public sealed class BorgJobSpawnTest
{
    // Declared as plain strings, not ProtoId<JobPrototype>: these only exist in [TestPrototypes], and the
    // YAML linter validates every ProtoId field against the real prototype set.
    private const string CarveOutJob = "OmuSpawnCarveOutTester";
    private const string JobEntityJob = "OmuSpawnJobEntityTester";
    private static readonly ProtoId<JobPrototype> Borg = "Borg";

    [TestPrototypes]
    private const string Prototypes = @"
# SharedJobSystem.SetupTrackerLookup keys a dictionary by playTimeTracker, so every job needs its own.
- type: playTimeTracker
  id: PlayTimeOmuSpawnJobEntity

- type: playTimeTracker
  id: PlayTimeOmuSpawnCarveOut

# Baseline: a jobEntity alone still short-circuits the character spawn path.
- type: job
  id: OmuSpawnJobEntityTester
  playTimeTracker: PlayTimeOmuSpawnJobEntity
  jobEntity: MobHuman

# What the borg job does: keep the jobEntity, but spawn through the character path with an overridden species.
- type: job
  id: OmuSpawnCarveOutTester
  playTimeTracker: PlayTimeOmuSpawnCarveOut
  jobEntity: MobHuman
  useCharacterSpawn: true
  speciesOverride: Dwarf
";

    [Test]
    public async Task JobDataDrivesSpawnPath()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var spawningSystem = entManager.System<StationSpawningSystem>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // Without the opt-in, a job with a jobEntity is spawned as that entity and never sees a species.
            var plain = spawningSystem.SpawnPlayerMob(testMap.GridCoords, job: JobEntityJob, profile: null, station: null);
            Assert.That(entManager.GetComponent<MetaDataComponent>(plain).EntityPrototype?.ID,
                Is.EqualTo("MobHuman"),
                "A job with only a jobEntity should still take the job entity shortcut.");

            // With useCharacterSpawn the shortcut is skipped, and speciesOverride then decides what is spawned.
            var carveOut = spawningSystem.SpawnPlayerMob(testMap.GridCoords, job: CarveOutJob, profile: null, station: null);
            Assert.That(entManager.GetComponent<MetaDataComponent>(carveOut).EntityPrototype?.ID,
                Is.EqualTo("MobDwarf"),
                "useCharacterSpawn + speciesOverride should spawn the overridden species' prototype, not the jobEntity.");

            entManager.DeleteEntity(plain);
            entManager.DeleteEntity(carveOut);
        });

        // The real borg job must carry the data, otherwise the behaviour above is not what borgs get.
        var borg = server.ProtoMan.Index(Borg);
        Assert.Multiple(() =>
        {
            Assert.That(borg.UseCharacterSpawn, Is.True, "The borg job must opt into the character spawn path.");
            Assert.That(borg.SpeciesOverride?.Id, Is.EqualTo("Cyborg"), "The borg job must override its species.");
        });

        await pair.CleanReturnAsync();
    }
}

using System.Collections.Generic;
using System.Linq;
using Content.Server.Maps;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

#pragma warning disable IDE1006 // Naming Styles - dumb bs that looks worse than the yellow line itself so...
namespace Content.IntegrationTests.Tests._Omu.Maps;
#pragma warning restore IDE1006 // Naming Styles - ...maybe should remove it and suck it up?

[TestFixture]
public sealed class MapPoolTests
{

    /// <summary>
    /// Gets a pool's maps from its YAML after inheritance, so it should also work for abstract pools.
    /// </summary>
    private static HashSet<string> GetMapsFromYaml(IPrototypeManager ptManager, string poolId)
    {
        // since this is run inside a multi-assertion, it will crash the test if the pool doesn't exist...
        if (!ptManager.TryGetMapping(typeof(GameMapPoolPrototype), poolId, out var mapping)) 
        { 
            Assert.Fail($"Map pool {poolId} does not exist!");
            // ...so we return an empty set to avoid further errors in the test, but it still counts as a failure when it's finished.
            return new HashSet<string>();
        }

        if (!mapping!.TryGet<SequenceDataNode>("maps", out var mapsNode))
            return new HashSet<string>();

        return mapsNode.Select(n => ((ValueDataNode) n).Value).ToHashSet();
    }
    /// <summary>
    /// General test of the map pool inheritance system. Checks that all pools that inherit from other pools contain all of their parents' maps.
    /// </summary>
    [Test]
    public async Task PoolsContainAllTheirParentsMaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var ptManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var inheritingPools = ptManager.EnumeratePrototypes<GameMapPoolPrototype>()
                .Where(p => p.Parents is { Length: > 0 })
                .ToList();

            // If nothing inherits, the checks below would pass without checking anything.
            Assert.That(inheritingPools, Is.Not.Empty, "YAMLSlop Warning: No map pools have a parent!");

            Assert.Multiple(() =>
            {
                foreach (var pool in inheritingPools)
                {
                    foreach (var parentId in pool.Parents!)
                    {
                        // Read the parent from YAML so abstract parents work too (they can't be Index'd in the prototype manager).
                        var parentMaps = GetMapsFromYaml(ptManager, parentId);
                        var missing = parentMaps.Except(pool.Maps).ToList();

                        Assert.That(missing, Is.Empty,
                            $"Map pool {pool.ID} is missing maps from parent {parentId}: {string.Join(", ", missing)}");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }
    /// <summary>
    /// The shared pool that omu uses for its map pools. Should be updated if the shared pool is ever changed, and should be abstract so it can't be used as a pool itself.
    /// See: <code>Resources/Prototypes/_Omu/Maps/Pools/servers.yml</code>
    /// </summary>
    private const string SHARED_POOL_ID = "OmuSharedMapPool";
    /// <summary>
    /// Test of omu-specific map pool inheritance. Separate from the above test because it is more specific to omu's intended use of map pools.
    /// </summary>
    [Test]
    public async Task OmuSpecificSharedPoolExistsAndIsAbstract()
    {

        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var ptManager = server.ResolveDependency<IPrototypeManager>();

        // The shared pool should exist in YAML...
        ptManager.TryGetMapping(typeof(GameMapPoolPrototype), SHARED_POOL_ID, out var sharedPool);
        // ...and it should have a list of maps, even if it's empty...
        Assert.That(sharedPool!.TryGet<SequenceDataNode>("maps", out var sharedPoolMapsNode), $"YAMLSlop Warning: Shared pool {SHARED_POOL_ID} has no maps list! You can add an empty list if you want! (Ex: maps: [])");
        Assert.That(sharedPoolMapsNode, Is.Not.Null, $"YAMLSlop Warning: Shared pool {SHARED_POOL_ID} has a null maps list!");
        await server.WaitAssertion(() =>
        {
            // ...buuuut since said shared pool is abstract, it should not be in the list of map pools/usable as a pool at runtime.
            var sharedPoolID = new ProtoId<GameMapPoolPrototype>(SHARED_POOL_ID);
            var mapPools = ptManager.EnumeratePrototypes<GameMapPoolPrototype>();
            Assert.That(mapPools.Any((pool) => pool.ID == sharedPoolID), Is.False, $"YAMLSlop Warning: Shared pool {SHARED_POOL_ID} should not be in the list of map pools and should be abstract.");
        });
        await pair.CleanReturnAsync();

    }
}
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
    private const string SHARED_POOL_ID = "OmuSharedMapPool";

    [Test]
    public async Task ServerPoolsInheritSharedPool()
    {

        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var ptManager = server.ResolveDependency<IPrototypeManager>();

        // The shared pool should exist in YAML...
        ptManager.TryGetMapping(typeof(GameMapPoolPrototype), SHARED_POOL_ID, out var sharedPool);
        Assert.That(sharedPool, Is.Not.Null, $"YAMLSlop Warning: Shared pool {SHARED_POOL_ID} does not exist in YAML!");
        // ...and it should have a list of maps, even if it's empty...
        Assert.That(sharedPool!.TryGet<SequenceDataNode>("maps", out var sharedPoolMapsNode), $"YAMLSlop Warning: Shared pool {SHARED_POOL_ID} has no maps list! You can add an empty list if you want! (Ex: maps: [])");

        await server.WaitAssertion(() =>
        {
            // ...buuuut since said shared pool is abstract, it should not be in the list of map pools/usable as a pool at runtime.
            var sharedPoolID = new ProtoId<GameMapPoolPrototype>(SHARED_POOL_ID);
            var mapPools = ptManager.EnumeratePrototypes<GameMapPoolPrototype>();
            Assert.That(mapPools.Any((pool) => pool.ID == sharedPoolID), Is.False, $"YAMLSlop Warning: Shared pool {SHARED_POOL_ID} should not be in the list of map pools and should be abstract.");

            // Get list of shared maps from the YAML
            var sharedPoolMaps = sharedPoolMapsNode!.Select(n => ((ValueDataNode) n).Value).ToHashSet();

            // Check that all non-abstract pools that have the shared pool as a parent have all of the shared maps in their own map list.
            Assert.Multiple(() =>
            {
                foreach (var pool in mapPools)
                {
                    if (pool.Parents == null)
                        continue;
                    if (pool.Parents.Contains(sharedPoolID))
                    {
                        // Might not need this. I don't even think it ever gets here.
                        // if (pool.Abstract)
                        // {
                        //     // While there are prob some valid reasons to have an abstract pool inherit from another, it's probably not intended?
                        //     Assert.Warn($"YAMLSlop Warning: Map pool {pool.ID} has the shared pool {SHARED_POOL_ID} as a parent but is abstract itself and cannot be used as a map pool! Did you mean to make it non-abstract?");
                        // }
                        var poolMaps = pool.Maps.ToHashSet();
                        Assert.That(poolMaps.IsSupersetOf(sharedPoolMaps), Is.True, $"Map pool {pool.ID} should have all maps from the shared pool {SHARED_POOL_ID}!");
                    }
                }
            });

        });

    }
}
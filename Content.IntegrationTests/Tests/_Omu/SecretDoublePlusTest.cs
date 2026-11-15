using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Server.StationEvents.Components;
using Content.Omu.Server.StationEvents.SecretDoublePlus;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Omu;

[TestFixture]
public sealed class SecretDoublePlusTest
{
    [Test]
    public async Task ValidateChaosScores()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFac = server.ResolveDependency<IComponentFactory>();
        var entMan = server.ResolveDependency<IEntityManager>();
        var entTable = entMan.System<EntityTableSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                var protos = protoMan.EnumeratePrototypes<EntityPrototype>()
                    .Where(p => !p.Abstract)
                    .ToList();

                var checkedEvs = new HashSet<EntProtoId>();

                foreach (var proto in protos)
                {
                    if (!proto.TryGetComponent<SecretDoublePlusComponent>(out var secretDoublePlusComp, compFac))
                        continue;

                    if (!proto.TryGetComponent<SelectedGameRulesComponent>(out var selectedComp, compFac))
                        continue;

                    foreach (var ev in entTable.GetSpawns(selectedComp.ScheduledGameRules))
                        CheckEvent(ev);

                    foreach (var (ev, _) in protoMan.Index(secretDoublePlusComp.RoundStartAntagsWeightTable).Weights)
                        CheckEvent(ev);

                    foreach (var (ev, _) in protoMan.Index(secretDoublePlusComp.PrimaryAntagsWeightTable).Weights)
                        CheckEvent(ev);

                    void CheckEvent(EntProtoId id)
                    {
                        if (!checkedEvs.Add(id))
                            return;

                        var evProto = protoMan.Index(id);

                        evProto.TryGetComponent<GameRuleComponent>(out var ruleComp, compFac);

                        Assert.That(ruleComp, Is.Not.Null, $"Entity prototype {id} is used as gamerule by {proto.ID}, but has no GameRuleComponent!");

                        bool any = false;
                        if (evProto.TryGetComponent<AntagSelectionComponent>(out var selection, compFac))
                        {
                            any = selection.Definitions.Any(def => def.ChaosScore != null);
                            if (any)
                                Assert.That(selection.Definitions.All(def => def.ChaosScore != null), Is.True, $"Gamerule {id} is fireable by {proto.ID}, but only some of its antag selection definitions have a chaos score!");
                        }

                        // allow null chaos score if we have non-null chaos score on antag selections
                        if (!any)
                            Assert.That(ruleComp.ChaosScore, Is.Not.Null, $"Gamerule {id} is fireable by {proto.ID}, but has no chaos score!");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestSecretDoublePlusStarts()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            InLobby = true,
        });
        var server = pair.Server;

        // Add a large number of dummy players so we have enough for some antag rolls
        await server.AddDummySessions(50);
        await server.WaitRunTicks(5);

        // Set up the game ticker and set everyone to ready
        var gameTicker = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GameTicker>();
        gameTicker.ToggleReadyAll(true);

        // Start the game rule and let it run for a few ticks
        await server.WaitAssertion(() =>
        {
            gameTicker.AddGameRule("SecretDoublePlusMid");
            gameTicker.StartGamePresetRules();
        });
        await server.WaitRunTicks(3);

        // Assert that we started some game rules
        await server.WaitAssertion(() =>
        {
            Assert.That(gameTicker.GetAddedGameRules().Count(), Is.GreaterThan(1), $"No additional rules started by secret double plus rule.");
            gameTicker.ClearGameRules();
        });

        await pair.CleanReturnAsync();
    }
}

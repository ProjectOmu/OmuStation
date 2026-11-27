// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Client.SetSelector;
using Content.Goobstation.Shared.SetSelector;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Omu;

[TestFixture]
public sealed class SetSelectorTests
{
    [Test]
    public async Task PlaytimeLockedSetsRequirePlaytime()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var playtime = server.ResolveDependency<PlayTimeTrackingManager>();
        var session = server.PlayerMan.Sessions.Single();
        var map = await pair.CreateTestMap();
        var cases = new List<(SetSelectorBoundUserInterfaceState State, Dictionary<string, TimeSpan> Times, int Index)>();
        await PoolManager.WaitUntil(server, () => playtime.TryGetTrackerTimes(session, out _));

        await server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, actor);
            var times = playtime.GetTrackerTimes(session);
            var factory = server.ResolveDependency<IComponentFactory>();
            var tested = 0;

            foreach (var casePrototype in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (casePrototype.Abstract ||
                    !casePrototype.TryGetComponent<SetSelectorComponent>(out var caseSelector, factory))
                    continue;

                var lockedSets = caseSelector.PossibleSets.Select(id => prototypes.Index(id))
                    .Where(set => set.Requirements.Count > 0).ToArray();
                foreach (var set in lockedSets)
                {
                    var requiredTimes = new Dictionary<string, TimeSpan>();
                    foreach (var requirement in set.Requirements)
                    {
                        Assert.That(requirement.Inverted, Is.False, $"{set.ID}: expected a minimum playtime requirement");
                        var (tracker, time) = requirement switch
                        {
                            DepartmentTimeRequirement department =>
                                (prototypes.Index(prototypes.Index(department.Department).Roles.First()).PlayTimeTracker, department.Time),
                            RoleTimeRequirement role => (role.Role.Id, role.Time),
                            _ => throw new NotSupportedException($"Unsupported test requirement: {requirement.GetType().Name}")
                        };
                        requiredTimes[tracker] = time > requiredTimes.GetValueOrDefault(tracker)
                            ? time : requiredTimes[tracker];
                    }

                    void SetTimes(TimeSpan offset)
                    {
                        times.Clear();
                        foreach (var (tracker, time) in requiredTimes)
                            times[tracker] = time + offset;
                    }

                    times.Clear();
                    var caseUid = entities.SpawnEntity(casePrototype.ID, map.GridCoords);
                    var selector = entities.GetComponent<SetSelectorComponent>(caseUid);
                    var index = selector.AvailableSets.ToList().FindIndex(id => id.Id == set.ID);
                    Assert.That(index, Is.GreaterThanOrEqualTo(0), casePrototype.ID);
                    var state = new SetSelectorBoundUserInterfaceState(selector.AvailableSets
                        .Select((id, number) => (Set: prototypes.Index(id), Index: number))
                        .ToDictionary(entry => entry.Index, entry => new SelectableSetInfo(
                            entry.Set.Name, entry.Set.Description, entry.Set.Sprite, false, entry.Set.ID)), selector.MaxSelectedSets);
                    cases.Add((state, requiredTimes, index));

                    // Unrestricted choices still work, and closing clears them.
                    var ordinary = selector.AvailableSets.ToList().FindIndex(id => prototypes.Index(id).Requirements.Count == 0);
                    if (ordinary >= 0)
                    {
                        entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorChangeSetMessage(ordinary) { Actor = actor });
                        Assert.That(selector.SelectedSets, Is.EqualTo(new[] { ordinary }));
                        entities.EventBus.RaiseLocalEvent(caseUid, new BoundUIClosedEvent(SetSelectorUIKey.Key, caseUid, actor));
                        Assert.That(selector.SelectedSets, Is.Empty);
                    }

                    // Invalid indices and insufficient playtime leave the case intact.
                    foreach (var invalid in new[] { -1, selector.AvailableSets.Count })
                        entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorChangeSetMessage(invalid) { Actor = actor });
                    SetTimes(-TimeSpan.FromSeconds(1));
                    Assert.That(set.CanSelect(session, playtime, entities, prototypes, out var reason), Is.False, set.ID);
                    Assert.That(reason, Is.Not.Null.And.Not.Empty);
                    entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorChangeSetMessage(index) { Actor = actor });
                    Assert.That(selector.SelectedSets, Is.Empty);
                    Assert.That(entities.Deleted(caseUid), Is.False);

                    SetTimes(TimeSpan.Zero);
                    Assert.That(set.CanSelect(session, playtime, entities, prototypes, out _), Is.True, set.ID);
                    entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorChangeSetMessage(index) { Actor = actor });
                    Assert.That(selector.SelectedSets, Is.EqualTo(new[] { index }));
                    var contentsBefore = set.Content.Distinct().ToDictionary(id => id.Id,
                        id => entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == id.Id));

                    // Approval rechecks playtime before spawning any contents.
                    times.Clear();
                    entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorApproveMessage { Actor = actor });
                    Assert.That(entities.Deleted(caseUid), Is.False);
                    foreach (var (id, count) in contentsBefore)
                        Assert.That(entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == id), Is.EqualTo(count));

                    SetTimes(TimeSpan.Zero);
                    entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorApproveMessage { Actor = actor });
                    Assert.That(entities.Deleted(caseUid), Is.True);
                    foreach (var content in set.Content.GroupBy(id => id.Id))
                        Assert.That(entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == content.Key),
                            Is.GreaterThanOrEqualTo(contentsBefore[content.Key] + content.Count()), set.ID);
                    tested++;
                }
            }

            Assert.That(tested, Is.GreaterThan(0), "No playtime-locked selector options were tested.");
        });

        // Exercise the menu just below, at, and above each configured requirement.
        foreach (var testCase in cases)
        foreach (var offset in new[] { -TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromSeconds(1) })
        {
            var state = testCase.State;
            var allowed = new Dictionary<int, bool>();
            await server.WaitPost(() =>
            {
                var times = playtime.GetTrackerTimes(session);
                times.Clear();
                foreach (var (tracker, time) in testCase.Times)
                    times[tracker] = time + offset;
                foreach (var (index, info) in state.Sets)
                    allowed[index] = prototypes.Index(info.Prototype).CanSelect(session, playtime, entities, prototypes, out _);
                playtime.QueueSendTimers(session);
            });
            await pair.RunTicksSync(5);
            await pair.Client.WaitAssertion(() =>
            {
                var clientTimes = pair.Client.ResolveDependency<ISharedPlaytimeManager>().GetPlayTimes(pair.Client.Session!);
                foreach (var (tracker, time) in testCase.Times)
                    Assert.That(clientTimes[tracker], Is.EqualTo(time + offset));
                using var menu = new SetSelectorMenu();
                menu.UpdateState(state);
                var rows = menu.FindControl<BoxContainer>("SetsGrid").Children.Cast<SelectableSet>().ToArray();
                foreach (var (index, canSelect) in allowed)
                    Assert.That(rows[index].SetButton.Disabled, Is.EqualTo(!canSelect), state.Sets[index].Prototype.Id);
                Assert.That(rows[testCase.Index].ToolTip, Is.Not.Null.And.Not.Empty);
                Assert.That(menu.ApproveButton.Disabled, Is.True);
                if (!allowed[testCase.Index])
                    return;

                var selected = state.Sets[testCase.Index];
                selected.Selected = true;
                state.Sets[testCase.Index] = selected;
                menu.UpdateState(state);
                Assert.That(menu.ApproveButton.Disabled, Is.False);
                selected.Selected = false;
                state.Sets[testCase.Index] = selected;
            });
        }

        await pair.CleanReturnAsync();
    }
}

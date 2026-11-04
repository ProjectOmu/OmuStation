// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Client.SetSelector;
using Content.Goobstation.Shared.SetSelector;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Goobstation;

[TestFixture]
public sealed class SetSelectorTests
{
    [Test]
    public async Task GoldenDutyWeaponsRequirePlaytime()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var playtime = server.ResolveDependency<PlayTimeTrackingManager>();
        var session = server.PlayerMan.Sessions.Single();
        var map = await pair.CreateTestMap();
        var states = new List<SetSelectorBoundUserInterfaceState>();
        var officer = prototypes.Index<JobPrototype>("SecurityOfficer").PlayTimeTracker;
        await PoolManager.WaitUntil(server, () => playtime.TryGetTrackerTimes(session, out _));

        await server.WaitAssertion(() =>
        {
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, actor);
            var times = playtime.GetTrackerTimes(session);
            var warden = prototypes.Index<JobPrototype>("Warden").PlayTimeTracker;

            foreach (var caseId in new[] { "OfficerGunCase", "BrigMedCase" })
            {
                times.Clear();
                var caseUid = entities.SpawnEntity(caseId, map.GridCoords);
                var selector = entities.GetComponent<SetSelectorComponent>(caseUid);
                states.Add(new SetSelectorBoundUserInterfaceState(selector.AvailableSets
                    .Select((id, index) => (Set: prototypes.Index(id), Index: index))
                    .ToDictionary(entry => entry.Index, entry => new SelectableSetInfo(
                        entry.Set.Name, entry.Set.Description, entry.Set.Sprite, false, entry.Set.ID)), 1));
                Assert.That(selector.AvailableSets.TakeLast(2).Select(id => id.Id),
                    Is.EqualTo(new[] { "WeaponMk58GoldSet", "WeaponT8GoldSet" }));

                // Ordinary choices still work, and closing clears them.
                entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorChangeSetMessage(0) { Actor = actor });
                Assert.That(selector.SelectedSets, Is.EqualTo(new[] { 0 }));
                entities.EventBus.RaiseLocalEvent(caseUid, new BoundUIClosedEvent(SetSelectorUIKey.Key, caseUid, actor));
                Assert.That(selector.SelectedSets, Is.Empty);

                // Invalid indices and locked choices do not select or consume anything.
                foreach (var index in new[] { -1, selector.AvailableSets.Count, selector.AvailableSets.Count - 1 })
                    entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorChangeSetMessage(index) { Actor = actor });
                Assert.That(selector.SelectedSets, Is.Empty);
                Assert.That(entities.Deleted(caseUid), Is.False);

                foreach (var id in selector.AvailableSets.TakeLast(2))
                {
                    var set = prototypes.Index(id);
                    times[officer] = TimeSpan.FromHours(70) - TimeSpan.FromSeconds(1);
                    Assert.That(set.CanSelect(session, playtime, entities, prototypes, out var reason), Is.False);
                    Assert.That(reason, Is.Not.Null.And.Not.Empty);
                    // Department time is cumulative across Security jobs.
                    times[officer] = TimeSpan.FromHours(69);
                    times[warden] = TimeSpan.FromHours(1);
                    Assert.That(set.CanSelect(session, playtime, entities, prototypes, out _), Is.True);
                    times.Clear();
                }

                var goldenIndex = selector.AvailableSets.Count - 1;
                var weaponsBefore = entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == "SecurityDutyRevolverGold");
                var loadersBefore = entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == "SpeedLoaderRevolverLightRifle");
                times[officer] = TimeSpan.FromHours(70);
                entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorChangeSetMessage(goldenIndex) { Actor = actor });
                Assert.That(selector.SelectedSets, Is.EqualTo(new[] { goldenIndex }));

                // Approval rechecks the actor rather than trusting the earlier selection.
                times.Clear();
                entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorApproveMessage { Actor = actor });
                Assert.That(entities.Deleted(caseUid), Is.False);
                Assert.That(entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == "SecurityDutyRevolverGold"), Is.EqualTo(weaponsBefore));
                times[officer] = TimeSpan.FromHours(70);
                entities.EventBus.RaiseLocalEvent(caseUid, new SetSelectorApproveMessage { Actor = actor });
                Assert.That(entities.Deleted(caseUid), Is.True);
                Assert.That(entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == "SecurityDutyRevolverGold"), Is.EqualTo(weaponsBefore + 1));
                Assert.That(entities.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == "SpeedLoaderRevolverLightRifle"), Is.EqualTo(loadersBefore + 2));
                var weapon = entities.EntityQuery<MetaDataComponent>().First(meta => meta.EntityPrototype?.ID == "SecurityDutyRevolverGold").Owner;
                var ammo = new GetAmmoCountEvent();
                entities.EventBus.RaiseLocalEvent(weapon, ref ammo);
                Assert.That(ammo.Count, Is.EqualTo(8));
                Assert.That(ammo.Capacity, Is.EqualTo(8));
                Assert.That(entities.GetComponent<GunComponent>(weapon).FireRate, Is.EqualTo(3));
            }
        });

        // Exercise the actual client menu using the server's synchronized playtime.
        foreach (var hours in new[] { 0, 69, 70, 71 })
        {
            await server.WaitPost(() =>
            {
                var times = playtime.GetTrackerTimes(session);
                times.Clear();
                times[officer] = TimeSpan.FromHours(hours);
                playtime.QueueSendTimers(session);
            });
            await pair.RunTicksSync(5);
            await pair.Client.WaitAssertion(() =>
            {
                var clientTimes = pair.Client.ResolveDependency<ISharedPlaytimeManager>().GetPlayTimes(pair.Client.Session!);
                Assert.That(clientTimes[officer], Is.EqualTo(TimeSpan.FromHours(hours)));
                using var menu = new SetSelectorMenu();
                foreach (var state in states)
                {
                    menu.UpdateState(state);
                    var rows = menu.FindControl<BoxContainer>("SetsGrid").Children.Cast<SelectableSet>().ToArray();
                    Assert.That(rows.TakeLast(2).All(row => row.SetButton.Disabled == (hours < 70)), Is.True);
                    Assert.That(rows.SkipLast(2).All(row => !row.SetButton.Disabled), Is.True);
                    Assert.That(rows.Last().ToolTip, Does.Contain("70"));
                    Assert.That(menu.ApproveButton.Disabled, Is.True);
                    if (hours < 70)
                        continue;

                    var index = state.Sets.Count - 1;
                    var selected = state.Sets[index];
                    selected.Selected = true;
                    state.Sets[index] = selected;
                    menu.UpdateState(state);
                    Assert.That(menu.ApproveButton.Disabled, Is.False);
                    selected.Selected = false;
                    state.Sets[index] = selected;
                }
            });
        }

        await pair.CleanReturnAsync();
    }
}

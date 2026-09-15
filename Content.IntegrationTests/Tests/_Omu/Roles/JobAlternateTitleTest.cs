// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Omu.Roles;
using Content.Shared.Roles;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Omu.Roles;

[TestFixture]
[TestOf(typeof(JobAlternateTitlePrototype))]
public sealed class JobAlternateTitleTest
{
    [Test]
    public async Task AlternateTitlePrototypesAreValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var localization = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            var jobNames = prototypes.EnumeratePrototypes<JobPrototype>()
                .Select(job => job.LocalizedName)
                .ToHashSet();
            var seen = new HashSet<(ProtoId<JobPrototype>, string)>();

            Assert.Multiple(() =>
            {
                foreach (var title in prototypes.EnumeratePrototypes<JobAlternateTitlePrototype>())
                {
                    Assert.That(prototypes.HasIndex(title.Job), Is.True, $"{title.ID} references missing job {title.Job}");
                    Assert.That(localization.HasString(title.Name), Is.True, $"{title.ID} has no localized string {title.Name}");

                    var name = title.LocalizedName;
                    Assert.That(jobNames, Does.Not.Contain(name), $"{title.ID} reuses the name of an existing job: {name}");
                    Assert.That(seen.Add((title.Job, name)), Is.True, $"{title.ID} duplicates another title of {title.Job}: {name}");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}

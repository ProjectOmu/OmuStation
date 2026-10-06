// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Antag.Components;

namespace Content.IntegrationTests.Tests._Omu;

[TestFixture]
[TestOf(typeof(AntagSelectionComponent))]
public sealed class AntagJobImmunityOverrideTest
{
    /// <summary>
    /// A job listed in both jobAntagImmunityOverride and jobBlacklist is usually silently ignored.
    /// GetPlayersJobCandidates checks the blacklist first and skips the job before the override
    /// is ever read, so the override has no effect and nothing logs a warning. This is likely
    /// unexpected behavior, so this test kinda only exists to gatekeep having a job in both lists.
    /// </summary>
    [Test]
    public async Task NoJobInBothOverrideAndBlacklist()
    {
        // boots up a server and client to get the prototypes, but doesn't actually run a round or anything
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.GetPrototypesWithComponent<AntagSelectionComponent>();
        // find all antag prototypes that have both a jobAntagImmunityOverride and a jobBlacklist
        var antagDefsToTest = prototypes
            .SelectMany(proto => proto.Item2.Definitions
                .Select((def, index) => (FullAntagProto: proto.Item1, AntagDef: def, Index: index)))
            .Where(x => x.AntagDef.JobAntagImmunityOverride != null && x.AntagDef.JobBlacklist != null);
        
        Assert.Multiple(() =>
        {
            foreach (var (fullAntagProto, antagDef, index) in antagDefsToTest)
            {
                var blacklistSet = antagDef.JobBlacklist.ToHashSet(); 
                var overlap = antagDef.JobAntagImmunityOverride.Where(blacklistSet.Contains).Select(j => j.Id).ToList(); // find any jobs that are in both lists (should be empty)
                Assert.That(overlap, Is.Empty, $"Antag prototype {fullAntagProto} has job(s) {string.Join(", ", overlap)} in both jobAntagImmunityOverride and jobBlacklist! Please check its YAML.");
            }
        });

        await pair.CleanReturnAsync();
    }
}
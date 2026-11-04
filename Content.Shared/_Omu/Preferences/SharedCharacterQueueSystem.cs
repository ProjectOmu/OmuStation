// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Preferences;

public abstract class SharedCharacterQueueSystem : EntitySystem
{
    public HumanoidCharacterProfile? GetPrimaryCharacter(PlayerPreferences prefs, CharacterQueueState queue)
    {
        return GetActiveCharacters(prefs, queue).FirstOrDefault()
            ?? GetOrderedSlots(prefs, queue)
                .Select(slot => prefs.Characters[slot])
                .OfType<HumanoidCharacterProfile>()
                .FirstOrDefault();
    }

    public IEnumerable<int> GetOrderedSlots(PlayerPreferences prefs, CharacterQueueState queue)
    {
        return queue.Order
            .Where(prefs.Characters.ContainsKey)
            .Concat(prefs.Characters.Keys.Except(queue.Order).OrderBy(slot => slot));
    }

    protected IEnumerable<HumanoidCharacterProfile> GetActiveCharacters(
        PlayerPreferences prefs,
        CharacterQueueState queue)
    {
        return GetOrderedSlots(prefs, queue)
            .Where(queue.ActiveSlots.Contains)
            .Select(slot => prefs.Characters[slot])
            .OfType<HumanoidCharacterProfile>();
    }

    protected List<HumanoidCharacterProfile> GetCandidates(
        PlayerPreferences prefs,
        CharacterQueueState queue,
        ProtoId<JobPrototype> job)
    {
        var candidates = GetActiveCharacters(prefs, queue).Where(c => c.JobPriorities.ContainsKey(job)).ToList();
        if (candidates.Count == 0 && GetPrimaryCharacter(prefs, queue) is { } primary)
            candidates.Add(primary);

        return candidates;
    }

    protected static int Weight(HumanoidCharacterProfile character, ProtoId<JobPrototype> job)
    {
        return character.JobPriorities.GetValueOrDefault(job) switch
        {
            JobPriority.High => 3,
            JobPriority.Medium => 2,
            _ => 1,
        };
    }
}

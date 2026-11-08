// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.Lobby;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._Omu.Preferences;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Omu.Preferences;

public sealed class CharacterQueueSystem : SharedCharacterQueueSystem
{
    [Dependency] private readonly IClientPreferencesManager _prefs = default!;
    [Dependency] private readonly JobRequirementsManager _requirements = default!;

    public CharacterQueueState State { get; private set; } = new([], [], []);

    public event Action? Updated;

    private uint _sequence;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CharacterQueueStateEvent>(OnState);
    }

    public List<HumanoidCharacterProfile> GetJoinCharacters(JobPrototype job, out FormattedMessage reason)
    {
        reason = FormattedMessage.FromUnformatted(Loc.GetString("character-queue-no-character-for-job"));
        var allowed = new List<HumanoidCharacterProfile>();

        if (_prefs.Preferences is not { } prefs)
            return allowed;

        foreach (var candidate in GetCandidates(prefs, State, job.ID))
        {
            if (_requirements.IsAllowed(job, candidate, out var failure))
                allowed.Add(candidate);
            else
                reason = failure;
        }

        return allowed;
    }

    public List<(HumanoidCharacterProfile Character, float Chance)> GetJobChances(JobPrototype job)
    {
        if (_prefs.Preferences is not { } prefs)
            return [];

        var takers = GetActiveCharacters(prefs, State)
            .Where(c => c.JobPriorities.ContainsKey(job.ID) && _requirements.IsAllowed(job, c, out _))
            .ToList();

        var total = takers.Sum(c => Weight(c, job.ID));
        return takers.Select(c => (c, (float) Weight(c, job.ID) / total)).ToList();
    }

    public void SetJobPriority(ProtoId<JobPrototype> job, JobPriority priority)
    {
        var priorities = new Dictionary<ProtoId<JobPrototype>, JobPriority>(State.JobPriorities);
        if (priority == JobPriority.High)
        {
            foreach (var (other, otherPriority) in State.JobPriorities)
            {
                if (otherPriority == JobPriority.High)
                    priorities[other] = JobPriority.Medium;
            }
        }

        if (priority == JobPriority.Never)
            priorities.Remove(job);
        else
            priorities[job] = priority;

        Send(new CharacterQueueState(priorities, State.ActiveSlots, State.Order));
    }

    public void SetActive(int slot, bool active)
    {
        var slots = new HashSet<int>(State.ActiveSlots);
        if (active)
            slots.Add(slot);
        else
            slots.Remove(slot);

        Send(new CharacterQueueState(State.JobPriorities, slots, State.Order));
    }

    public void MoveCharacter(int slot, int index)
    {
        if (_prefs.Preferences is not { } prefs)
            return;

        var order = GetOrderedSlots(prefs, State).ToList();
        if (!order.Remove(slot))
            return;

        order.Insert(Math.Clamp(index, 0, order.Count), slot);
        Send(new CharacterQueueState(State.JobPriorities, State.ActiveSlots, order));
    }

    private void Send(CharacterQueueState state)
    {
        State = state;
        RaiseNetworkEvent(new SetCharacterQueueEvent(state, ++_sequence));
        Updated?.Invoke();
    }

    private void OnState(CharacterQueueStateEvent ev)
    {
        var changed = !ev.State.ActiveSlots.SetEquals(State.ActiveSlots)
            || !ev.State.Order.SequenceEqual(State.Order)
            || ev.State.JobPriorities.Count != State.JobPriorities.Count
            || ev.State.JobPriorities.Any(pair => State.JobPriorities.GetValueOrDefault(pair.Key) != pair.Value);

        State = ev.State;
        if (changed)
            Updated?.Invoke();
    }
}

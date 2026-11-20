// SPDX-License-Identifier: MIT

using Content.Shared.Dataset;
using Content.Shared.Item;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private readonly HashSet<Entity<ItemComponent>> _fetchable = new();

    private void NoticeCome(Entity<CrewletBrainComponent> ent, string message, TimeSpan now)
    {
        var brain = ent.Comp;
        if (HeardWord(brain.ComeWords, message) is not { } word)
            return;

        brain.Summoned = true;
        brain.StayUntil = TimeSpan.Zero;
        Echo(ent, word, now);
        if (brain.Activity is >= CrewletActivity.Hug and < CrewletActivity.Follow)
            End(ent);
    }

    private void NoticeStay(Entity<CrewletBrainComponent> ent, string message, TimeSpan now)
    {
        var brain = ent.Comp;
        if (HeardWord(brain.StayWords, message) is not { } word)
            return;

        brain.Summoned = false;
        brain.FetchItem = null;
        brain.StayUntil = now + brain.StayTime;
        Echo(ent, word, now);
    }

    private string? HeardWord(ProtoId<LocalizedDatasetPrototype> words, string message)
    {
        foreach (var word in _proto.Index(words).Values)
        {
            var text = Loc.GetString(word);
            if (ContainsWord(message, text))
                return text;
        }

        return null;
    }

    private void Echo(Entity<CrewletBrainComponent> ent, string command, TimeSpan now)
    {
        ent.Comp.PendingLine = Loc.GetString(ent.Comp.CommandEcho, ("command", command));
        StartTyping(ent, now + ent.Comp.TypingTime);
    }

    private void RunStay(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Step == CrewletStep.Start)
            WaitFor(ent, ent.Comp.StayUntil - now, now);

        if (now >= ent.Comp.StayUntil)
            End(ent);
    }

    private void NoticeFetch(Entity<CrewletBrainComponent> ent, string message, TimeSpan now)
    {
        var brain = ent.Comp;
        if (!IsReady(brain, CrewletActivity.Fetch, now))
            return;

        _fetchable.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, brain.FetchRange, _fetchable, LookupFlags.Uncontained);

        EntityUid? best = null;
        var bestScore = -1;
        var bestDistance = float.PositiveInfinity;
        foreach (var item in _fetchable)
        {
            var score = FetchScore(brain, message, Name(item));
            if (score < bestScore || score < 0 || _whitelist.IsWhitelistPass(brain.LootBlacklist, item))
                continue;

            var distance = DistanceTo(ent, item);
            if (score == bestScore && distance >= bestDistance)
                continue;

            best = item;
            bestScore = score;
            bestDistance = distance;
        }

        _fetchable.Clear();
        if (best == null)
            return;

        brain.FetchItem = best;
        brain.FetchUntil = now + brain.FetchMemory;
        var name = Name(best.Value);
        Echo(ent, bestScore > 0 ? name : LastWord(name), now);
    }

    private static int FetchScore(CrewletBrainComponent brain, string message, string name)
    {
        if (ContainsWord(message, name))
            return name.Length;

        var last = LastWord(name);
        return last.Length >= brain.MinFetchWord && last.Length < name.Length && ContainsWord(message, last) ? 0 : -1;
    }

    private static string LastWord(string name)
    {
        return name[(name.LastIndexOf(' ') + 1)..];
    }

    private bool WantsFetch(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.FetchItem is not { } item)
            return false;

        if (now >= brain.FetchUntil || !Present(item) || _container.IsEntityInContainer(item))
        {
            brain.FetchItem = null;
            return false;
        }

        if (HeldItem(ent) != null || !IsReady(brain, CrewletActivity.Fetch, now))
            return false;

        brain.Candidate = item;
        brain.FetchItem = null;
        return true;
    }

    private void RunFetch(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Target is not { } item || _container.IsEntityInContainer(item))
        {
            End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Start)
        {
            Point(ent, item);
            Begin(ent, item, brain.MeleeRange, now);
        }

        if (!Reach(ent, now, out _))
            return;

        Interact(ent, item);
        if (_hands.IsHolding(ent.Owner, item))
            brain.GiftRecipient = brain.KnownOwner;

        End(ent);
    }
}

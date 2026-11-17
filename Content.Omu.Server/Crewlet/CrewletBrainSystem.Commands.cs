// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared.Item;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private readonly HashSet<Entity<ItemComponent>> _fetchable = new();

    private void NoticeCome(Entity<CrewletBrainComponent> ent, string message)
    {
        var brain = ent.Comp;
        if (!_proto.Index(brain.ComeWords).Values.Any(word => ContainsWord(message, Loc.GetString(word))))
            return;

        brain.Summoned = true;
        if (brain.Activity is >= CrewletActivity.Hug and < CrewletActivity.Follow)
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
    }

    private static int FetchScore(CrewletBrainComponent brain, string message, string name)
    {
        if (ContainsWord(message, name))
            return name.Length;

        var last = name[(name.LastIndexOf(' ') + 1)..];
        return last.Length >= brain.MinFetchWord && last.Length < name.Length && ContainsWord(message, last) ? 0 : -1;
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

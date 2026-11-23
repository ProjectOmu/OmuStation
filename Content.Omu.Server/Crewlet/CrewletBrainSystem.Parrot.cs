// SPDX-License-Identifier: MIT

using Content.Shared.Chat;
using Robust.Shared.Random;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private void OnOwnerEmote(Entity<CrewletOwnerComponent> ent, ref EmoteEvent args)
    {
        if (_crewletQuery.HasComp(ent))
            return;

        var now = _timing.CurTime;
        foreach (var uid in ent.Comp.Crewlets)
        {
            if (!TryComp<CrewletBrainComponent>(uid, out var brain)
                || !brain.Active
                || brain.PendingEmote != null
                || !_random.Prob(brain.ParrotChance)
                || !InSight((uid, brain), ent))
                continue;

            brain.PendingEmote = args.Emote.ID;
            brain.PendingEmoteAt = now + _random.Next(brain.MinParrotDelay, brain.MaxParrotDelay);
        }
    }

    private void UpdateParrot(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.PendingEmote is not { } emote || now < brain.PendingEmoteAt)
            return;

        brain.PendingEmote = null;
        if (!_chat.TryEmoteWithChat(ent, emote, forceEmote: true))
            return;

        brain.Emotes.Remove(emote);
        while (brain.Emotes.Count > 0 && brain.Emotes.Count >= brain.MaxEmoteMemory)
        {
            brain.Emotes.RemoveAt(0);
        }

        brain.Emotes.Add(emote);
    }

    private void RunMimic(Entity<CrewletBrainComponent> ent)
    {
        var emotes = ent.Comp.Emotes;
        if (emotes.Count > 0)
            _chat.TryEmoteWithChat(ent, emotes[_random.Next(emotes.Count)], forceEmote: true);

        End(ent);
    }
}

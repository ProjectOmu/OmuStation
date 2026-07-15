using Content.Shared.Chat; // Einstein Engines - Languages
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server._Shitmed.ItemSwitch;
using Content.Shared.Weapons.Melee.Events;
using Content.Omu.Server.EmoteOnMeleeHit.Components;
using Content.Server.Chat.Systems;


namespace Content.Omu.Server.EmoteOnMeleeHit.Systems;

public sealed class EmoteOnMeleeHitSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmoteOnMeleeHitComponent, MeleeHitEvent>(OnHit, before: [typeof(ItemSwitchSystem)]);
    }

    private void OnHit(Entity<EmoteOnMeleeHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!TryComp(ent, out EmoteOnMeleeHitComponent? emoteOnMeleeHit))
            return;


        foreach (var hit in args.HitEntities)
        {
            if (emoteOnMeleeHit.LastEmoteTime + emoteOnMeleeHit.EmoteCooldown > _gameTiming.CurTime)
                return;

            if (emoteOnMeleeHit.Emotes.Count == 0)
                return;

            if (!_random.Prob(emoteOnMeleeHit.EmoteChance))
                return;

            var emote = _random.Pick(emoteOnMeleeHit.Emotes);
            if (emoteOnMeleeHit.WithChat)
                _chatSystem.TryEmoteWithChat(hit, emote, emoteOnMeleeHit.HiddenFromChatWindow ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal);
            else
                _chatSystem.TryEmoteWithoutChat(hit, emote, voluntary: false);

            emoteOnMeleeHit.LastEmoteTime = _gameTiming.CurTime;
        }
    }

    /// <summary>
    /// Try to add an emote to the entity, which will be performed at an interval.
    /// </summary>
    public bool AddEmote(EntityUid uid, string emotePrototypeId, EmoteOnMeleeHitComponent? emoteOnMeleeHit = null)
    {
        if (!Resolve(uid, ref emoteOnMeleeHit, logMissing: false))
            return false;

        DebugTools.Assert(emoteOnMeleeHit.LifeStage <= ComponentLifeStage.Running);
        DebugTools.Assert(_prototypeManager.HasIndex<EmotePrototype>(emotePrototypeId), "Prototype not found. Did you make a typo?");

        return emoteOnMeleeHit.Emotes.Add(emotePrototypeId);
    }

    /// <summary>
    /// Stop preforming an emote. Note that by default this will queue empty components for removal.
    /// </summary>
    public bool RemoveEmote(EntityUid uid, string emotePrototypeId, EmoteOnMeleeHitComponent? emoteOnMeleeHit = null, bool removeEmpty = true)
    {
        if (!Resolve(uid, ref emoteOnMeleeHit, logMissing: false))
            return false;

        DebugTools.Assert(_prototypeManager.HasIndex<EmotePrototype>(emotePrototypeId), "Prototype not found. Did you make a typo?");

        if (!emoteOnMeleeHit.Emotes.Remove(emotePrototypeId))
            return false;

        if (removeEmpty && emoteOnMeleeHit.Emotes.Count == 0)
            RemCompDeferred(uid, emoteOnMeleeHit);

        return true;
    }
}

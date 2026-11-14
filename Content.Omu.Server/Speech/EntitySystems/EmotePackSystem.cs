// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Barks;
using Content.Omu.Common.Changeling;
using Content.Omu.Server.Speech.Components;
using Content.Omu.Shared.Speech;
using Content.Server._Omu.Preferences;
using Content.Server.Antag;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Omu.Speech;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Cloning.Events;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Speech.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Omu.Server.Speech.EntitySystems;

public sealed class EmotePackSystem : SharedEmotePackSystem
{
    [Dependency] private readonly CharacterQueueSystem _characterQueue = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<EmotePackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EmotePackComponent, EmoteEvent>(OnEmote,
            before: [typeof(VocalSystem), typeof(MumbleAccentSystem)]);
        SubscribeLocalEvent<EmotePackComponent, CloningEvent>(OnCloning);
        SubscribeLocalEvent<BorrowedVoiceComponent, MapInitEvent>(OnBorrowedVoiceMapInit);
        SubscribeLocalEvent<BorrowedVoiceComponent, CloningEvent>(OnBorrowedVoiceCloning);
        SubscribeLocalEvent<VocalComponent, ChangelingFormAssumedEvent>(OnFormAssumed);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        ApplyProfile(ev.Mob, ev.Profile);
    }

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session is { } session && _characterQueue.GetCurrentCharacter(session.UserId) is { } profile)
            ApplyProfile(args.EntityUid, profile);
    }

    private void ApplyProfile(EntityUid mob, HumanoidCharacterProfile profile)
    {
        if (CompOrNull<HumanoidAppearanceComponent>(mob)?.Species != profile.Species)
            return;

        if (EmotePackRules.TryGetChoice(_proto, _factory, profile, out var pack, out var source))
            AddComp(mob, new EmotePackComponent { Pack = pack.ID, Species = source }, true);

        if (_proto.TryIndex(profile.BarkVoice, out var bark)
            && bark.SpeciesWhitelist?.Any(s =>
                VoiceUnlockPrototype.IsUnlocked(_proto, profile, s, emotePack: false)) == true)
            AddComp(mob, new BorrowedVoiceComponent { Bark = profile.BarkVoice }, true);
    }

    private void OnMapInit(Entity<EmotePackComponent> ent, ref MapInitEvent args)
    {
        if (!TryGetSource(ent.Comp, out var pack, out var source))
            return;

        if (source.TryGetComponent<TagComponent>(out var tags, _factory))
        {
            var emoteTags = _proto.EnumeratePrototypes<EmotePrototype>()
                .SelectMany(emote => emote.Whitelist?.Tags ?? [])
                .ToHashSet();
            _tag.AddTags(ent, tags.Tags.Where(emoteTags.Contains));
        }

        if (!TryComp<VocalComponent>(ent, out var vocal))
            return;

        var speciesSounds = EmotePackRules.SpeciesSounds(_proto, _factory, ent.Comp.Species);
        foreach (var sex in Enum.GetValues<Sex>())
        {
            var sounds = pack.SoundsFor(sex, speciesSounds);
            if (sounds.Count > 0)
                ent.Comp.Sounds[sex] = sounds;
        }

        if (ent.Comp.Sounds.Count == 0)
            return;

        vocal.Sounds = ent.Comp.Sounds.ToDictionary(pair => pair.Key, pair => pair.Value[0]);
        Dirty(ent, vocal);
        var ev = new SoundsChangedEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnEmote(Entity<EmotePackComponent> ent, ref EmoteEvent args)
    {
        var sex = CompOrNull<HumanoidAppearanceComponent>(ent)?.Sex ?? Sex.Unsexed;
        if (!TryComp<VocalComponent>(ent, out var vocal)
            || !ent.Comp.Sounds.TryGetValue(sex, out var sounds)
            || vocal.Sounds?.GetValueOrDefault(sex) != sounds[0])
            return;

        vocal.EmoteSounds = _random.Pick(sounds);
    }

    private void OnCloning(Entity<EmotePackComponent> ent, ref CloningEvent args)
    {
        AddComp(args.CloneUid, new EmotePackComponent { Pack = ent.Comp.Pack, Species = ent.Comp.Species }, true);
    }

    private void OnBorrowedVoiceMapInit(Entity<BorrowedVoiceComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        humanoid.BarkVoice = ent.Comp.Bark;
        Dirty(ent, humanoid);
        EnsureComp<SpeechSynthesisComponent>(ent, out var synthesis);
        synthesis.VoicePrototypeId = ent.Comp.Bark;
        Dirty(ent, synthesis);
    }

    private void OnBorrowedVoiceCloning(Entity<BorrowedVoiceComponent> ent, ref CloningEvent args)
    {
        AddComp(args.CloneUid, new BorrowedVoiceComponent { Bark = ent.Comp.Bark }, true);
    }

    private void OnFormAssumed(Entity<VocalComponent> ent, ref ChangelingFormAssumedEvent args)
    {
        if (TryComp<EmotePackComponent>(args.Original, out var pack))
            AddComp(ent, new EmotePackComponent { Pack = pack.Pack, Species = pack.Species }, true);

        if (TryComp<BorrowedVoiceComponent>(args.Original, out var voice))
            AddComp(ent, new BorrowedVoiceComponent { Bark = voice.Bark }, true);
    }
}

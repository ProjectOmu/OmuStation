// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Speech.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Speech;

public static class EmotePackRules
{
    public static EntityPrototype? SpeciesMob(IPrototypeManager proto, ProtoId<SpeciesPrototype> species)
    {
        return proto.TryIndex(species, out var speciesPrototype) ? proto.Index(speciesPrototype.Prototype) : null;
    }

    public static IReadOnlyDictionary<Sex, ProtoId<EmoteSoundsPrototype>> SpeciesSounds(IPrototypeManager proto,
        IComponentFactory factory,
        ProtoId<SpeciesPrototype> species)
    {
        if (SpeciesMob(proto, species) is { } mob
            && mob.TryGetComponent<VocalComponent>(out var vocal, factory)
            && vocal.Sounds is { } sounds)
            return sounds;

        return new Dictionary<Sex, ProtoId<EmoteSoundsPrototype>>();
    }

    public static List<EmotePackPrototype> PacksFor(IPrototypeManager proto,
        IComponentFactory factory,
        ProtoId<SpeciesPrototype> species)
    {
        var packs = proto.EnumeratePrototypes<EmotePackPrototype>().ToList();
        var own = packs.Where(pack => pack.Species == species).ToList();
        var sounds = SpeciesSounds(proto, factory, species);
        if (own.Count > 0 || sounds.Count == 0)
            return own;

        var unisex = sounds.GetValueOrDefault(Sex.Male) == sounds.GetValueOrDefault(Sex.Female);
        return packs.Where(pack => pack.Species == null && pack.Unisex == unisex).ToList();
    }

    public static bool TryGetChoice(IPrototypeManager proto,
        IComponentFactory factory,
        HumanoidCharacterProfile profile,
        [NotNullWhen(true)] out EmotePackPrototype? pack,
        out ProtoId<SpeciesPrototype> source)
    {
        source = profile.EmotePackSpecies ?? profile.Species;
        if (!proto.TryIndex(profile.EmotePack, out pack))
            return false;

        source = pack.Species ?? source;
        return PacksFor(proto, factory, source).Contains(pack)
            && (source == profile.Species
                || VoiceUnlockPrototype.IsUnlocked(proto, profile, source, emotePack: true));
    }
}

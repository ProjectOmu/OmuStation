// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Speech;

[Prototype]
public sealed partial class EmotePackPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public ProtoId<SpeciesPrototype>? Species;

    [DataField]
    public bool Unisex;

    [DataField]
    public Dictionary<Sex, ProtoId<EmoteSoundsPrototype>> Sounds = new();

    [DataField]
    public List<Sex> Sexes = new();

    public List<ProtoId<EmoteSoundsPrototype>> SoundsFor(Sex sex,
        IReadOnlyDictionary<Sex, ProtoId<EmoteSoundsPrototype>> speciesSounds)
    {
        if (Sounds.TryGetValue(sex, out var sounds))
            return new() { sounds };

        return Sexes.Where(speciesSounds.ContainsKey).Select(s => speciesSounds[s]).Distinct().ToList();
    }
}

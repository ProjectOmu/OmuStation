// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Speech;

[Prototype]
public sealed partial class VoiceUnlockPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public ProtoId<TraitPrototype>? Trait;

    [DataField]
    public List<ProtoId<SpeciesPrototype>> InnateSpecies = new();

    [DataField]
    public List<ProtoId<SpeciesPrototype>>? Species;

    [DataField]
    public List<ProtoId<SpeciesPrototype>> Blacklist = new();

    [DataField]
    public bool EmotePacks = true;

    public static bool IsUnlocked(IPrototypeManager proto,
        HumanoidCharacterProfile profile,
        ProtoId<SpeciesPrototype> species,
        bool emotePack)
    {
        return proto.EnumeratePrototypes<VoiceUnlockPrototype>()
            .Any(unlock => (unlock.Trait is { } trait && profile.TraitPreferences.Contains(trait)
                    || unlock.InnateSpecies.Contains(profile.Species))
                && (unlock.Species == null || unlock.Species.Contains(species))
                && !unlock.Blacklist.Contains(species)
                && (unlock.EmotePacks || !emotePack));
    }
}

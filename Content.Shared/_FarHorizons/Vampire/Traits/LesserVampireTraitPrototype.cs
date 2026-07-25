using Content.Shared._Starlight.Traits.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Vampire.Traits;

[Prototype]
public sealed partial class LesserVampireTraitPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public LocId Description;
    [DataField(required: true)] public int Cost;
    [DataField] public List<ProtoId<LesserVampireTraitPrototype>> Incompatible = new();
    [DataField] public List<BaseTraitEffect> Effects = new();
}

using Content.Shared.Dataset;
using Content.Shared.Damage;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Impstation.Thaven;

[Prototype("thavenMoodConfig")]
[Serializable, NetSerializable]
public sealed partial class ThavenMoodConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<DatasetPrototype> SharedDataset;

    [DataField(required: true)]
    public ProtoId<DatasetPrototype> YesAndDataset;

    [DataField(required: true)]
    public ProtoId<DatasetPrototype> NoAndDataset;

    [DataField(required: true)]
    public ProtoId<DatasetPrototype> WildcardDataset;

    [DataField(required: true)]
    public EntProtoId ViewMoodsAction;

    [DataField(required: true)]
    public ProtoId<WeightedRandomPrototype> RandomMoodDataset;

    [DataField]
    public float EmagDoAfterSeconds = 1f;

    [DataField]
    public DamageSpecifier EmagDamage = new();
}

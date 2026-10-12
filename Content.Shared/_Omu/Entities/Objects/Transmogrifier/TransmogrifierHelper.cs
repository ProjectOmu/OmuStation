using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Entities.Objects.Transmogrifier;

[Prototype]
public sealed partial class TransmogrifierGraphPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public TransmogrifierResult Coarse = new();
    [DataField]
    public TransmogrifierResult Rough = new();

    [DataField]
    public TransmogrifierResult OneToOne = new();

    [DataField]
    public TransmogrifierResult Fine = new();

    [DataField]
    public TransmogrifierResult VeryFine = new();

    [DataField(required: true)]
    public EntProtoId TransformedItem = "";
}

[DataDefinition]
public sealed partial class TransmogrifierResult
{
    /// <summary>
    /// A fixed entity to spawn. Used when no weighted options are configured.
    /// </summary>
    [DataField]
    public EntProtoId? Entity;

    /// <summary>
    /// Optional weighted entity choices.
    /// </summary>
    [DataField]
    public List<TransmogrifierWeightedEntry> Weighted = new();
}

[DataDefinition]
public sealed partial class TransmogrifierWeightedEntry
{
    [DataField(required: true)]
    public EntProtoId Entity = string.Empty;

    [DataField(required: true)]
    public float Weight;
}

using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Entities.Objects.Transmogrifier;

[Prototype]
public sealed partial class TransmogrifierGraphPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public EntProtoId Coarse = "Ash";
    [DataField]
    public EntProtoId Rough = "Ash";

    [DataField]
    public EntProtoId OneToOne = "Ash";

    [DataField]
    public EntProtoId Fine = "Ash";

    [DataField]
    public EntProtoId VeryFine = "Ash";

    [DataField(required: true)]
    public EntProtoId TransformedItem = "";
}

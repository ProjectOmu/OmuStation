using Robust.Shared.Prototypes;
using Content.Shared.Materials;

namespace Content.Omu.Server._BSD.SignalSilo.Prototypes;

[Prototype("MaterialFillPrototype")]
public sealed partial class MaterialFillPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> FillGoal = new();
}
[Prototype]
public sealed partial class MaterialFillListPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;
    [DataField]
    public HashSet<ProtoId<MaterialFillPrototype>> Options = new();

    public MaterialFillListPrototype()
    {
        return;
    }
}
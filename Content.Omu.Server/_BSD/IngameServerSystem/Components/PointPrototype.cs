using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.IngameServerSystem.Prototype;

/// <summary>
/// Prototype representing a struct Types in YAML.
/// Meant to only have an ID property, as that is the only thing that
/// gets saved in hashsets.
/// </summary>
[Prototype("ServerPoint")]
public sealed partial class IngameServerPointPrototypePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;
}
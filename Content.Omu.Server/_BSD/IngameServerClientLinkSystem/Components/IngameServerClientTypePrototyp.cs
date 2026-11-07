using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.IngameServerClientLinkSystem.Prototype;

/// <summary>
/// Prototype representing a struct Types in YAML.
/// Meant to only have an ID property, as that is the only thing that
/// gets saved in hashsets.
/// </summary>
[Prototype("ServerClientPrototype")]
public sealed partial class IngameServerClientPrototypePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;
}
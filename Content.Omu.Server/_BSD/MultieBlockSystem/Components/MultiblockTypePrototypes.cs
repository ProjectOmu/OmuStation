using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.MultiBlockSystem.Components;

/// <summary>
/// Prototype representing a struct Types in YAML.
/// Meant to only have an ID property, as that is the only thing that
/// gets saved in Multistruct hashsets.
/// </summary>
[Prototype("MultiStructType")]
public sealed partial class MultiStructTypePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;
}
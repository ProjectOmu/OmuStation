using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Content.Omu.Server._BSD.SignalSilo.Prototypes;

namespace Content.Omu.Server._BSD.SignalSilo.Components;

[RegisterComponent]
public sealed partial class SignalSiloLathegradeMaterialContainerComponent : Component
{
    /// <summary>
    /// total amount of material that can be stored in this container
    /// </summary>
    [DataField]
    public int Capacity = 30000;
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> Stored = new();
}

[RegisterComponent]
public sealed partial class SignalSiloLathegradeMaterialContainerFillerComponent : Component
{
    /// <summary>
    /// Configures if materials can get lost during transit from silo to container
    /// </summary>
    [DataField]
    public float Effiency = 1.0f;
    [DataField]
    public IngameServerPointPrototypePrototype? CurrentFillMode = null;
}
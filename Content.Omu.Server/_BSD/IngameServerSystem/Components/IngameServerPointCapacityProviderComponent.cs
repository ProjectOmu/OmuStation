using Content.Omu.Server._BSD.IngameServerSystem.Prototype;
using Content.Omu.Server._BSD.MultiBlockSystem.Components;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.IngameServerSystem.Components;

[RegisterComponent]

public sealed partial class IngameServerPointCapacityComponent : Component
{
    /// <summary>
    /// The amount of additional points this allowes to store the "server" 
    /// 
    /// ALL provided point types need to be declared in this !!!
    /// it wont work even if you add scaling for a point type!!!
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<IngameServerPointPrototypePrototype>, int> CapacityExpansion = new();

    /// <summary>
    /// Determines if this entity scales via a multistruct functions.
    /// Sets the CacacityExpansion number based on size. aka TYPES need to be declared!!!!(idealy as type: 0)
    /// </summary>
    [DataField]
    public bool MultistructScaling = false;

    [DataField]
    public float MaxMultistructPower = 10.0f;

    [DataField]
    public Dictionary<ProtoId<IngameServerPointPrototypePrototype>, int> CapacityBaseline = new();
    [DataField]
    public Dictionary<ProtoId<IngameServerPointPrototypePrototype>, int> CapacityLinearGrowth = new();
    [DataField]
    public Dictionary<ProtoId<IngameServerPointPrototypePrototype>, int> CapacityQuadraticGrowth = new();

    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> CapacityProvidingTypes = new();
}
using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.MultiBlockSystem.Components;

[RegisterComponent]
public sealed partial class MultiBlockEnergyManagmentComponent : Component
{
    /// <summary>
    /// Energy stored in the machine in J
    /// </summary>
    [DataField]
    public Int64 StoredEnergy = 0;

    /// <summary>
    /// Max Energy stored in the machine in J
    /// </summary>
    [DataField]
    public Int64 StoredEnergyCapacity = 1000000;//1MJ default

    /// <summary>
    /// Energy drain in W
    /// </summary>
    [DataField]
    public Int64 EnergyDelta = 0;

    /// <summary>
    /// If a multistruct is powered
    /// </summary>
    [DataField]
    public bool Powered = false;

    /// <summary>
    /// expects the battery component
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> EnergyProvidingTypes = new();

    /// <summary>
    /// expects the multiblock energy storage comp
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> EnergyCapacityTypes = new();

}
[RegisterComponent]
public sealed partial class MultiBlockEnergyStorageComponent : Component
{
    /// <summary>
    /// Energy stored in the machine in J
    /// </summary>
    [DataField]
    public Int64 StoreEnergy = 20000000;//adds 20 MJ of energy storage

}
[RegisterComponent]
public sealed partial class MultiBlockEnergyTransfairComponent : Component
{
    /// <summary>
    /// Energy stored in the machine in J
    /// </summary>
    [DataField]
    public Int64 TransEnergy = 500000;//adds 0.5 MW positive value adds chage to system

}
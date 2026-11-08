using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.MultiBlockSystem.Components;
//TODO: should probably add a blacklist feature later on!!! -> note for thesia -> rewriting this rn so why not do this rn along side ajacency
[RegisterComponent]

public sealed partial class MultiBlockPartComponent : Component
{
    [DataField]
    public EntityUid? ConstrollEntity;
    /// <summary>
    /// Type of the machine, varries from multiblock to multiblock
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> PartTypes = new();

    /// <summary>
    /// in witch directions the block allowes additions to itself
    /// </summary>
    [DataField]
    public bool[] Connectability = [true, true, true, true];//N,E,S,W

    /// <summary>
    /// Allowed types this thing is allowed to connect to, the universal key is "ALL", current issue highly limited as only one per category, may be amended later
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> AllowedConnectionTypesNorth = new();
    /// <summary>
    /// Allowed types this thing is allowed to connect to, the universal key is "ALL", current issue highly limited as only one per category, may be amended later
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> AllowedConnectionTypesEast = new();
    /// <summary>
    /// Allowed types this thing is allowed to connect to, the universal key is "ALL", current issue highly limited as only one per category, may be amended later
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> AllowedConnectionTypesSouth = new();
    /// <summary>
    /// Allowed types this thing is allowed to connect to, the universal key is "ALL", current issue highly limited as only one per category, may be amended later
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> AllowedConnectionTypesWest = new();

    /// <summary>
    /// If a multiblock starts from this component
    /// </summary>
    [DataField]
    public bool Origin = false;

    /// <summary>
    /// If a multiblock already claimed this component
    /// </summary>
    [DataField]
    public bool Claimed = false;

    /// <summary>
    /// Ho much of a effect is transmitted from this block to the next
    /// </summary>
    [DataField]
    public float TransmissionEfficency = 0.98f;//warning this is EXPONENTIAL decrease handle with CARE and infuluences how effective machines are

    /// <summary>
    /// Ho much of a effect is transmitted from this block to the next
    /// </summary>
    [DataField]
    public float MachinePower = 1.0f;

}
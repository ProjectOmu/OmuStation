using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Omu.Server._BSD.MultiBlockSystem.Components;

[RegisterComponent]
public sealed partial class MultiBlockStructureCoreComponent : Component
{
    /// <summary>
    /// Prototype ID of the StructureControllComp
    /// </summary>
    [DataField]
    public EntProtoId? StructureCoreProdID = null;
}

[RegisterComponent]
//This is always present on origin blocks
public sealed partial class MultiBlockStructureComponent : Component
{
    [DataField]
    public EntityUid? LinkedOriginPart;
    /// <summary>
    /// if the structure can work
    /// </summary>
    [DataField]
    public bool Complete = false;

    /// <summary>
    /// how off center a entity can be and still be recognised, this is a radius NEVER have it above 0.5!!!
    /// </summary>
    [DataField]
    public float PositionErrorMargine = 0.3f;

    /// <summary>
    /// Components that can be added to the structure, connectors or upgrades
    /// </summary>
    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> AllowedTypes = new();

    /// <summary>
    /// Components Present
    /// </summary>
    [DataField]
    public Dictionary<string, float> TypesPresent = new Dictionary<string, float>();
    /// <summary>
    /// Components Present entity dic
    /// </summary>
    [DataField]
    public Dictionary<string, List<Node>>? EntityDic = new Dictionary<string, List<Node>>();

    /// <summary>
    /// List containing every existing positon on a relative grid 0|0 is bottom left
    /// </summary
    public Dictionary<string, bool?[,]> TypePresence2DMap = new();//NEVER add VV to this it will just crash
    [DataField]
    public int TypePresence2DMapDimentionX = 0;
    [DataField]
    public int TypePresence2DMapDimentionY = 0;

}

//[Serializable, NetSerializable]
public sealed class Node
{
    public float Efficency;
    public EntityUid Id;
    public string Type = "ERROR";
    public Vector2i LocRelativeGRid = new();

    public Node Clone()
    {
        return (Node) MemberwiseClone();
    }
}
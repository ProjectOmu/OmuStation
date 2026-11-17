using Content.Shared._Omu.SkillSystem;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.SkillSystem.Components;

/// <summary>
/// Component used on the mind --> local enity comp only to indicate the mind has this component
/// </summary>
[RegisterComponent]
public sealed partial class SkillHolderComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<SkillNamePrototype>, int> SkillDic = new();
    /// <summary>
    /// used by the Command skills underlings
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DepartmentNamePrototype>> Department = new();
    [DataField]
    public HashSet<ProtoId<DepartmentNamePrototype>> CommandSkillDepartment = new();
}
[RegisterComponent]
public sealed partial class SkillHolderBodyComponent : Component
{

}

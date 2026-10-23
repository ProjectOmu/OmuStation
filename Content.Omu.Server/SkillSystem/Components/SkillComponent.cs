using Content.Omu.Shared.SkillSystem;
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
}
[RegisterComponent]
public sealed partial class SkillHolderBodyComponent : Component
{

}

[RegisterComponent]
public sealed partial class SkillRequermentComponent : Component
{
    /// <summary>
    /// What skill is required to use this item
    /// OVERRIDE: NONE -> dismissed the check!!!!
    /// </summary>
    [DataField]
    public HashSet<ProtoId<SkillNamePrototype>> Skill = new();
    /// <summary>
    /// If not defined assume 1.0f
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SkillNamePrototype>, float> SpeedModifier = new();
    [DataField]
    public Dictionary<int, float> ParsedModifiers = new();

    /// <summary>
    /// If set to true cancels attempts that dont have the minimal skill level
    /// </summary>
    [DataField]
    public bool HardRequirment = false;
    [DataField]
    public ProtoId<SkillNamePrototype> HardRequirmentSkill = new();
    [DataField]
    public int HardRequirmentMinSkill = 1;
}
[RegisterComponent]
public sealed partial class CommandSkillComponent : Component
{
    [DataField]
    public float Range = 6.0f;
    [DataField]
    public HashSet<ProtoId<DepartmentNamePrototype>> EffectedDepartments = new();
    [DataField]
    public Dictionary<ProtoId<SkillNamePrototype>, float> SpeedModifier = new();
    [DataField]
    public Dictionary<int, float> ParsedSpeedModifier = new();
}
// <summary>
// This component should only be on CCOs and Mentor roles!!!!
/// </summary>
[RegisterComponent]
public sealed partial class SkillExaminPermittedComponent : Component
{

}

[RegisterComponent]
public sealed partial class SkillGrantingComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<SkillNamePrototype>, int> SkillDicMaxSkillLevel = new();
    [DataField]
    public bool DestroyAfterUse = true;
}

[RegisterComponent]
public sealed partial class SkillStaminaAjustmentComponent : Component
{
    [DataField]
    public HashSet<ProtoId<SkillNamePrototype>> Skill = new();
    [DataField]
    public Dictionary<ProtoId<SkillNamePrototype>, float> StaminaRegenModifiers = new();
    [DataField]
    public Dictionary<int, float> ParsedStaminaRegenModifiers = new();
}
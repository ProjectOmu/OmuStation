using Content.Shared._Omu.SkillSystem;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.SkillSystem.Components;

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
    public Dictionary<string, float> SpeedModifier = new();
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
    [DataField]
    public bool HideDesc = false;
}

[RegisterComponent]
public sealed partial class SkillStaminaAjustmentComponent : Component
{
    [DataField]
    public HashSet<ProtoId<SkillNamePrototype>> Skill = new();
    [DataField]
    public Dictionary<string, float> StaminaRegenModifiers = new();
    [DataField]
    public Dictionary<int, float> ParsedStaminaRegenModifiers = new();
}
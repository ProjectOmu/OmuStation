using Content.Shared._Omu.SkillSystem;
using Robust.Shared.Prototypes;
using Content.Shared.Humanoid.Prototypes;

namespace Content.Omu.Server.SkillSystem.Components;

[RegisterComponent]
public sealed partial class CommandSkillComponent : Component
{
    [DataField]
    public float Range = 8.0f;
    [DataField]
    public HashSet<ProtoId<DepartmentNamePrototype>> EffectedDepartments = new();
    [DataField]
    public Dictionary<string, float> SpeedModifier = new();
    [DataField]
    public Dictionary<int, float> ParsedSpeedModifier = new();
}
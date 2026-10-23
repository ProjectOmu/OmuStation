using Content.Omu.Shared.SkillSystem;
using Robust.Shared.Prototypes;
using Content.Shared.Humanoid.Prototypes;

namespace Content.Omu.Server.SkillSystem.Components;

[RegisterComponent]
public sealed partial class SpeciesBasedSkillAdditionComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<SpeciesPrototype>, Dictionary<ProtoId<SkillNamePrototype>, int>> SkillDic = new();
}

[RegisterComponent]
public sealed partial class RoundStartSkillGrantingComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<SkillNamePrototype>, int> SkillDic = new();
    [DataField]
    public HashSet<ProtoId<DepartmentNamePrototype>> Department = new();
}
[RegisterComponent]
public sealed partial class AntagSkillGrantingComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<SkillNamePrototype>, int> SkillDic = new();
}
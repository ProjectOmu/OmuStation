// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Skills.Prototypes;
using Content.Shared._Omu.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<SkillPrototype>, int> Skills = new();

    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<SkillDepartmentPrototype>> Departments = new();

    [DataField]
    public HashSet<ProtoId<SkillDepartmentPrototype>> Commands = new();
}

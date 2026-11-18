// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Skills.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillCommandComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<SkillDepartmentPrototype>> Departments = new();

    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;

    [DataField, AutoNetworkedField]
    public float Range = 8f;
}

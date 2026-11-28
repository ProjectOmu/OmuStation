// SPDX-License-Identifier: MIT

using Content.Shared._Omu.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SkillRequirementComponent : BaseSkillModifierComponent
{
    [DataField]
    public ProtoId<SkillPrototype>? RequiredSkill;

    [DataField]
    public int RequiredLevel = 1;

    [DataField]
    public bool HideExamine;
}

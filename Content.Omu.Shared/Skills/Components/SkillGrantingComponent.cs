// SPDX-License-Identifier: MIT

using Content.Shared._Omu.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SkillGrantingComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<SkillPrototype>, int> Skills = new();

    [DataField]
    public bool DestroyAfterUse = true;
}

// SPDX-License-Identifier: MIT

using Content.Shared._Omu.Skills;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Components;

public abstract partial class BaseSkillModifierComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<SkillPrototype>> Skills = new();

    [DataField(required: true)]
    public SortedDictionary<int, float> Modifiers = new();
}

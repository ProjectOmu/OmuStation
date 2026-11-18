// SPDX-License-Identifier: MIT

using Content.Shared._Omu.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SkillBodyComponent : Component
{
    [DataField]
    public HashSet<ProtoId<SkillPrototype>> StaminaSkills = new();

    [DataField]
    public SortedDictionary<int, float> StaminaModifiers = new();

    [DataField]
    public float StaminaMultiplier = 1f;

    [DataField]
    public HashSet<ProtoId<SkillPrototype>> CommandSkills = new();

    [DataField]
    public SortedDictionary<int, float> CommandModifiers = new();

    [DataField]
    public float CommandRange = 8f;
}

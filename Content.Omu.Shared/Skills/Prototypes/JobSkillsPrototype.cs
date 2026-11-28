// SPDX-License-Identifier: MIT

using Content.Shared._Omu.Skills;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Prototypes;

[Prototype]
public sealed partial class JobSkillsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public List<ProtoId<JobPrototype>> Jobs = new();

    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, int> Skills = new();

    [DataField]
    public HashSet<ProtoId<SkillPrototype>> Specialties = new();

    [DataField]
    public HashSet<ProtoId<SkillDepartmentPrototype>> Departments = new();

    [DataField]
    public HashSet<ProtoId<SkillDepartmentPrototype>> Commands = new();
}

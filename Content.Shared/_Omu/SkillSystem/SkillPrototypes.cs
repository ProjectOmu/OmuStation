// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.SkillSystem;

[Prototype]
public sealed partial class SkillNamePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string? Description;

    [DataField]
    public List<ProtoId<DepartmentPrototype>> JobDepartments = new();
}

[Prototype]
public sealed partial class DepartmentNamePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;
}

[Prototype]
public sealed partial class SkillLevelPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;
}

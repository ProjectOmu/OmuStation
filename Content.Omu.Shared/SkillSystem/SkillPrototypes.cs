// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.SkillSystem;

[Prototype]
public sealed partial class SkillNamePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string? Description;
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

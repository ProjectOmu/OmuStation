// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Prototypes;

[Prototype]
public sealed partial class SkillDepartmentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;
}

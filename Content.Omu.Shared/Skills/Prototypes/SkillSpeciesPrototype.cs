// SPDX-License-Identifier: MIT

using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Prototypes;

[Prototype]
public sealed partial class SkillSpeciesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public List<ProtoId<SpeciesPrototype>> Species = new();

    [DataField]
    public int Specialty;

    [DataField]
    public int Other;
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.SkillSystem;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.SkillSystem.Components;

[RegisterComponent]
public sealed partial class SkillAllocationComponent : Component
{
    [DataField]
    public int Points = 2;

    [DataField]
    public Dictionary<ProtoId<SkillNamePrototype>, int> Allocation = new();
}

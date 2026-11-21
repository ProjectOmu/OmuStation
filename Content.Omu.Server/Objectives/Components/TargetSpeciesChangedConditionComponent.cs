// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(TargetBodyConditionSystem))]
public sealed partial class TargetSpeciesChangedConditionComponent : Component
{
    [DataField]
    public ProtoId<SpeciesPrototype>? StartingSpecies;
}

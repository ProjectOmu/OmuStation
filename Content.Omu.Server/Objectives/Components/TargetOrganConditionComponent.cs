// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(TargetBodyConditionSystem))]
public sealed partial class TargetOrganConditionComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Organ;
}

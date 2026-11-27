// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(IdCardJobConditionSystem))]
public sealed partial class TargetJobLostConditionComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DepartmentPrototype> Department;
}

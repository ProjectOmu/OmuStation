// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(IdCardJobConditionSystem))]
public sealed partial class IdCardJobConditionComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<JobPrototype>> Jobs = new();
}

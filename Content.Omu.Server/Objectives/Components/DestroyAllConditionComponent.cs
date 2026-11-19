// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(DestroyAllConditionSystem))]
public sealed partial class DestroyAllConditionComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public EntityUid? Station;
}

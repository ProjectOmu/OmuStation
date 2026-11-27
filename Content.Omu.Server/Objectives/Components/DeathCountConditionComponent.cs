// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(DeathCountConditionSystem))]
public sealed partial class DeathCountConditionComponent : Component
{
    [DataField(required: true)]
    public int Required;

    [DataField]
    public int Deaths;
}

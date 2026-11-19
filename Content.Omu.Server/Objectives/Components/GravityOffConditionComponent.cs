// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(GravityOffConditionSystem))]
public sealed partial class GravityOffConditionComponent : Component
{
    [DataField(required: true)]
    public TimeSpan Duration;

    [DataField]
    public EntityUid? Grid;

    [DataField]
    public TimeSpan? LostAt;

    [DataField]
    public TimeSpan Longest;
}

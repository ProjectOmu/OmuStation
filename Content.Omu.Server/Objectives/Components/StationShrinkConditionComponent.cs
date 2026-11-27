// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(StationShrinkConditionSystem))]
public sealed partial class StationShrinkConditionComponent : Component
{
    [DataField(required: true)]
    public float RequiredLoss;

    [DataField]
    public EntityUid? Station;

    [DataField]
    public int StartingTiles;
}

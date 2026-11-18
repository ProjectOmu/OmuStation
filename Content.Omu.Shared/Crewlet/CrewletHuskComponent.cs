// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Omu.Shared.Crewlet;

[RegisterComponent, NetworkedComponent]
public sealed partial class CrewletHuskComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Users = new();

    [ViewVariables]
    public MapCoordinates DropAt = MapCoordinates.Nullspace;
}

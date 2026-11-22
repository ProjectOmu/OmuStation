// SPDX-License-Identifier: MIT

namespace Content.Omu.Server.Crewlet;

[RegisterComponent]
public sealed partial class CrewletOwnerComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Crewlets = new();
}

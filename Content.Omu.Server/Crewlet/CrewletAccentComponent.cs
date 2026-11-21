// SPDX-License-Identifier: MIT

namespace Content.Omu.Server.Crewlet;

[RegisterComponent]
public sealed partial class CrewletAccentComponent : Component
{
    [DataField]
    public string Name = string.Empty;
}

// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Server.Arcade;

[RegisterComponent, Access(typeof(PolybiusPullSystem))]
public sealed partial class PolybiusPullComponent : Component
{
    [DataField]
    public float Acceleration = 1.5f;

    [DataField]
    public float Range = 1.5f;
}

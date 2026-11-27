// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Server.Arcade;

[RegisterComponent, Access(typeof(PolybiusPullSystem))]
public sealed partial class PolybiusPulledComponent : Component
{
    public bool AddedNoRotate;
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Audio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HearingPressureComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [ViewVariables, AutoNetworkedField]
    public float Pressure = Atmospherics.OneAtmosphere;
}

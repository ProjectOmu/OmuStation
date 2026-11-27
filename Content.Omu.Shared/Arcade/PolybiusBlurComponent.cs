// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Arcade;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(PolybiusBlurSystem))]
public sealed partial class PolybiusBlurComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Blur = 4f;
}

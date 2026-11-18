// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Crewlet;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CrewletDisguiseComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Husk;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#c8323c");

    [DataField]
    public Color AuraColor = Color.FromHex("#ff1a1a8c");

    [DataField]
    public float AuraScale = 1.15f;
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.Arcade;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PolybiusLightFlickerComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;

    public bool? LightBefore;
}

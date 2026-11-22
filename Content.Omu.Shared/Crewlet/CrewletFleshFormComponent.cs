// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.Crewlet;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CrewletFleshFormComponent : Component
{
    [DataField]
    public EntityUid? Form;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan Until;
}

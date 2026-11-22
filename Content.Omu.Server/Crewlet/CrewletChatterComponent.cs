// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Crewlet;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CrewletChatterComponent : Component
{
    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(64);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextLine;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextEmote;
}

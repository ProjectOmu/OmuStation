// SPDX-License-Identifier: MIT

using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Crewlet;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CrewletHarmScreamComponent : Component
{
    [DataField]
    public ProtoId<EmotePrototype> Emote = "Scream";

    [DataField]
    public int Limit = 5;

    [DataField]
    public TimeSpan Window = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    [DataField]
    public List<TimeSpan> Screams = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan HushedUntil;
}

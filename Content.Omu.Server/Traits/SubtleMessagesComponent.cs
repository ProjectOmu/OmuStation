// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Traits;

[RegisterComponent, Access(typeof(SubtleMessagesSystem)), AutoGenerateComponentPause]
public sealed partial class SubtleMessagesComponent : Component
{
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Messages;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? StationAiMessages;

    [DataField(required: true)]
    public TimeSpan MaxTimeBetweenMessages;

    [DataField(required: true)]
    public TimeSpan MinTimeBetweenMessages;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextMessageTime;
}

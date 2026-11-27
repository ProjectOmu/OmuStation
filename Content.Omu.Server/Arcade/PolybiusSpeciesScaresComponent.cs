// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Arcade;

[RegisterComponent, Access(typeof(PolybiusSpeciesScaresSystem)), AutoGenerateComponentPause]
public sealed partial class PolybiusSpeciesScaresComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<SpeciesPrototype>, List<PolybiusSpeciesScare>> Scares = new();

    [DataField(required: true)]
    public TimeSpan MaxTimeBetweenScares;

    [DataField(required: true)]
    public TimeSpan MinTimeBetweenScares;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextScareTime;

    public PolybiusSpeciesScare? PendingScare;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan PendingScareTime;
}

[DataDefinition]
public sealed partial class PolybiusSpeciesScare
{
    [DataField]
    public ProtoId<EmotePrototype>? Emote;

    [DataField]
    public TimeSpan Delay;

    [DataField(required: true)]
    public LocId Popup;

    [DataField]
    public PopupType PopupType = PopupType.Medium;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float SoundDistance;

    [DataField]
    public EntProtoId? Spawn;
}

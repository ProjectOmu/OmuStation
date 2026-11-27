// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Traits;

[RegisterComponent, Access(typeof(HallucinationsSystem)), AutoGenerateComponentPause]
public sealed partial class HallucinationsComponent : Component
{
    [DataField]
    public EntProtoId StatusEffect = "StatusEffectSeeingRainbow";

    [DataField(required: true)]
    public TimeSpan MaxTimeBetweenIncidents;

    [DataField(required: true)]
    public TimeSpan MinTimeBetweenIncidents;

    [DataField(required: true)]
    public TimeSpan MaxDurationOfIncident;

    [DataField(required: true)]
    public TimeSpan MinDurationOfIncident;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextIncidentTime;
}

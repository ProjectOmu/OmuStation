// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Dataset;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.Arcade;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PolybiusFalseLawsComponent : Component
{
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> Words = "PolybiusFalseLaws";

    [DataField]
    public TimeSpan FlashDuration = TimeSpan.FromSeconds(0.75);

    [DataField(required: true)]
    public TimeSpan MaxTimeBetweenAlerts;

    [DataField(required: true)]
    public TimeSpan MinTimeBetweenAlerts;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextAlertTime;

    [DataField, AutoNetworkedField]
    public int Alerts;

    public int AlertsSeen;
}

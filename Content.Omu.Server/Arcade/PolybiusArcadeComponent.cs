// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Arcade;

[RegisterComponent, Access(typeof(PolybiusSystem)), AutoGenerateComponentPause]
public sealed partial class PolybiusArcadeComponent : Component
{
    [DataField]
    public EntProtoId StatusEffect = "StatusEffectPolybius";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(20);

    [DataField]
    public LocId PlayPopup = "polybius-arcade-play-popup";

    [DataField]
    public TimeSpan SpiralDuration = TimeSpan.FromSeconds(4);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? SpiralEndTime;
}

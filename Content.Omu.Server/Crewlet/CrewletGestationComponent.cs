// SPDX-License-Identifier: MIT

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Crewlet;

[RegisterComponent]
public sealed partial class CrewletGestationComponent : Component
{
    [DataField]
    public List<CrewletDelivery> Deliveries = new();

    [DataField]
    public List<CrewletHatch> Hatches = new();

    [DataField]
    public TimeSpan WarningDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan HatchDelay = TimeSpan.FromSeconds(7);

    [DataField]
    public EntProtoId GlowEffect = "CrewletOmenGlow";

    [DataField]
    public Color WarningColor = Color.FromHex("#a4262c");
}

[DataRecord]
public partial record struct CrewletDelivery(EntityUid Deliverer, FixedPoint2 Amount);

[DataDefinition]
public sealed partial class CrewletHatch
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan OmenAt;

    [DataField]
    public EntityUid Owner;

    [DataField]
    public CrewletHatchStage Stage;
}

public enum CrewletHatchStage : byte
{
    Gestating,
    Omen,
    Warned,
}

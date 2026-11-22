// SPDX-License-Identifier: MIT

using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Crewlet;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CrewletMergedAnomalyComponent : Component
{
    [DataField]
    public LocId ExamineText = "crewlet-merged-anomaly-examine";

    [DataField]
    public TimeSpan SpawnInterval = TimeSpan.FromSeconds(30);

    [DataField]
    public int SpawnCap = 6;

    [DataField]
    public float PointBonus = 1f;

    [DataField]
    public Color AuraColor = Color.FromHex("#ff1a1a");

    [DataField]
    public float AuraRadius = 3f;

    [DataField]
    public float AuraEnergy = 6f;

    [DataField]
    public EntProtoId MobKit = "CrewletMobKit";

    [DataField]
    public ProtoId<DamageModifierSetPrototype> MobProtection = "CrewletMobProtection";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextSpawn;
}

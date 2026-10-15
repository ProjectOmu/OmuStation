// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.Traits;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class DamagedThroatComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Asphyxiation", 1 },
            { "Blunt", 1 }
        }
    };

    [DataField]
    public int MaxEscalation = 4;

    [ViewVariables]
    public int Escalation;

    [DataField]
    public TimeSpan StrainCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan RecoveryTime = TimeSpan.FromSeconds(30);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LastStrainedAt;

    [DataField]
    public ProtoId<EmotePrototype> CoughEmote = "Cough";

    [DataField]
    public TimeSpan MinCoughDelay = TimeSpan.FromSeconds(0.3);

    [DataField]
    public TimeSpan MaxCoughDelay = TimeSpan.FromSeconds(0.7);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextCoughAt;

    [DataField]
    public ProtoId<EmotePrototype> ScreamEmote = "Scream";

    [DataField]
    public float VocalEmoteVolumeShift = -6f;

    [DataField, AutoNetworkedField]
    public FixedPoint2 NormalBreathingThreshold = 10;

    [DataField, AutoNetworkedField]
    public LocId StethoscopeReading = "stethoscope-damaged-throat";
}

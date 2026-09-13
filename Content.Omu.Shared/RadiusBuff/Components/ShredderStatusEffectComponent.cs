using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.RadiusBuff.Components;

/// <summary>
/// Component for the shredder buff status effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ShredderStatusEffectComponent : Component
{
    /// <summary>
    /// Healing provided, in intervals specified by Delay
    /// </summary>
    [DataField]
    public DamageSpecifier HealBuff = new()
    {
        DamageDict =
        {
            {"Blunt", -0.8f},
            {"Slash", -0.8f},
            {"Piercing", -0.8f},
            {"Heat", -0.5},
            {"Cold", -0.5},
            {"Shock", -0.5},
            {"Asphyxiation", -0.8},
            {"Bloodloss", -0.2},
        },
    };

    /// <summary>
    /// When the status effect will next.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextCheck = TimeSpan.Zero;

    /// <summary>
    /// Seconds between healing and visual effects.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Effect entity to use for the buff visual.
    /// </summary>
    [DataField]
    public EntProtoId? Visual = "EffectShredder";
}

using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.StationEvents.Components;

public abstract partial class LightsOutRuleComponent : Component
{
    /// <summary>
    /// When the actual smashing of lights should start
    /// </summary>
    [DataField(customTypeSerializer : typeof(TimeOffsetSerializer))]
    public TimeSpan? SmashingTime;

    /// <summary>
    /// The probability for an individual light source to be damaged
    /// </summary>
    [DataField]
    public virtual float DamageProbability { get; set; } = 0.7f;

    /// <summary>
    /// The damage to deal to the lights
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict =
        {
            ["Slash"] = 5,
        },
    };

    /// <summary>
    /// The selection of potential targets
    /// </summary>
    [DataField]
    public List<EntityUid> Targets = [];

    /// <summary>
    /// Index into Targets; used for staggered breaking
    /// </summary>
    [DataField]
    public int TargetIndex = 0;
}

using Content.Server.StationEvents.Events;
using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Omu.StationEvents.Components;

/// <summary>
/// This event announces a temporary power surge and then smashes many of the powered light sources on the station
/// </summary>
[RegisterComponent, Access(typeof(Events.LightsOutMajorRule))]
public sealed partial class LightsOutMajorRuleComponent : Component
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
    public float DamageProbability = 0.20f;

    /// <summary>
    /// The damage to deal to the lights
    /// </summary>
    public DamageSpecifier Damage;

    /// <summary>
    /// The selection of potential targets: Poweredlights that are on-station and powered
    /// </summary>
    public List<EntityUid> Targets = new();

    /// <summary>
    /// Length of the target list; used for staggered breaking
    /// </summary>
    public int TargetListLength;

    /// <summary>
    /// Index into Targets; used for staggered breaking
    /// </summary>
    public int TargetIndex = 0;
}

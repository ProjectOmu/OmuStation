using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.RadiusBuff.Components;

/// <summary>
/// Defines a status effect that an entity should give in a radius under some condition.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class RadiusBuffComponent : Component
{
    [DataField]
public bool Active;

    [DataField]
    public float Range = 4f;

    /// <summary>
    /// Seconds between application of the buff.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Duration of the buff. Buff will be continuous in range as long as this is more than delay.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// When this comp will next attempt to apply a buff
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextCheck = TimeSpan.Zero;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// If this buff should apply to the entity that is providing this buff, regardless of the whitelist.
    /// </summary>
    [DataField]
public bool BuffSelf;

    /// <summary>
    /// If this buff should apply to the entity that is holding the item providing this buff, regardless of the whitelist.
    /// Assumes that the entity with this component is an item.
    /// </summary>
    [DataField]
    public bool BuffHolder = true;

    /// <summary>
    /// Proto of the status effect to apply.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Status;
}

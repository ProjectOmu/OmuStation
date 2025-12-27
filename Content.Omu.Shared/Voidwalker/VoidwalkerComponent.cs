using Content.Shared.Damage;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using YamlDotNet.Core.Tokens;

namespace Content.Omu.Shared.Voidwalker;

[RegisterComponent, NetworkedComponent]
public sealed partial class VoidwalkerComponent : Component
{
    [DataField]
    public bool IsInSpace;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextSpacedCheck;

    [DataField]
    public TimeSpan SpacedCheckInterval = TimeSpan.FromSeconds(2);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextHealingTick;

    [DataField]
    public TimeSpan HealingTickInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How much to heal the voidwalker by when they're spaced.
    /// </summary>
    [DataField]
    public DamageSpecifier? HealingWhenSpaced;

    /// <summary>
    /// What to multiply the voidwalker's speed by when they're in a non-spaced area.
    /// </summary>
    [DataField]
    public float NonSpacedSpeedModifier = 0.7f;

    /// <summary>
    /// Any structures with these tags will not be collided with.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<TagPrototype>> PassableTags =
        [ "Window", "Grille" ];

    /// <summary>
    /// Components to add to a window that has been passed through.
    /// Yes, the method was named this in SS13.
    /// </summary>
    [DataField]
    public ComponentRegistry ComponentsAddedOnPass;

    /// <summary>
    /// A dictionary containing each entity that has had components added to it, and when it expires.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<EntityUid, TimeSpan> EntitiesPassed = new();

    /// <summary>
    /// How long do the components added to an object by passing through them last?
    /// </summary>
    [DataField]
    public TimeSpan EntityPassedAddedComponentsDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long you need to stare at someone to stun em
    /// </summary>
    [DataField]
    public TimeSpan UnsettleDoAfterDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan UnsettleStunDuration= TimeSpan.FromSeconds(4);

    [DataField]
    public float UnsettleStaminaDamage = 80f;

    [ViewVariables(VVAccess.ReadOnly)]
    public ushort? UnsettleDoAfterId;

}

using Content.Shared.Damage;

namespace Content.Omu.Shared.Voidwalker;

[RegisterComponent]
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
}

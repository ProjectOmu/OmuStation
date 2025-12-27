using Content.Omu.Shared.Voidwalker;

namespace Content.Omu.Server.Voidwalker.Kidnapping.Voided;

[RegisterComponent]
public sealed partial class VoidedComponent : Component
{
    /// <summary>
    /// The voidwalker that kidnapped this entity.
    /// </summary>
    [DataField]
    public Entity<VoidwalkerComponent>? Voidwalker;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextSpacedCheck;

    [DataField]
    public TimeSpan SpacedCheckInterval = TimeSpan.FromSeconds(2);
}

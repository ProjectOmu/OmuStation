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
}

namespace Content.Omu.Server.Voidwalker.CosmicSkull;

[RegisterComponent]
public sealed partial class CosmicSkullComponent : Component
{
    [DataField]
    public int Uses = 1;

    [DataField]
    public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(5);
}

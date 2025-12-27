namespace Content.Omu.Shared.Voidwalker.TemporarilyDisableCollision;

[RegisterComponent]
public sealed partial class TemporarilyDisableCollisionComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan EndTime;

    /// <summary>
    /// The range at which this entity will fling nearby objects when regaining collision to prevent
    /// anything getting stuck inside.
    /// </summary>
    [DataField]
    public int FlingRange = 1;

    /// <summary>
    /// The force of the fling.
    /// </summary>
    [DataField]
    public float FlingForce = 5;
}

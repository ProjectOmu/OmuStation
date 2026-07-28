using System.Numerics;

namespace Content.Server._Omu.Pinpointer;

/// <summary>
/// Handles non-movement alerts of salvage crew pinpointers.
/// </summary>
[RegisterComponent]
public sealed partial class SalvageCrewPinpointerComponent : Component
{
    /// <summary>
    /// How much time it takes until the pinpointer checks for movement again
    /// </summary>
    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Exactly when the next movement check is ran
    /// </summary>
    public TimeSpan NextCheck { get; set; }

    /// <summary>
    /// How many checks has the target failed in a row?
    /// </summary>
    [DataField]
    public int FailedChecks { get; set; }

    public Vector2 PreviousPositionWorld = Vector2.Zero;

    public Vector2i PreviousPositionTileLocal = Vector2i.Zero;
}

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.RadiusBuff.Components;

/// <summary>
/// Activates a <see cref="RadiusBuffComponent"/> when this entity is worn
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ActivateBuffOnWearComponent : Component
{
    /// <summary>
    /// Deactivate on wear instead
    /// </summary>
    [DataField]
    public bool Invert;
}

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.RadiusBuff.Components;

/// <summary>
/// Activates a <see cref="RadiusBuffComponent"/> when this entity is wielded
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ActivateBuffOnWieldComponent : Component
{
    /// <summary>
    /// Deactivate on wield instead
    /// </summary>
    [DataField]
public bool Invert;
}

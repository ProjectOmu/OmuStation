using Robust.Shared.GameStates;

namespace Content.Shared._Mono.NoHack;

/// <summary>
/// Prevents wire interactions (cutting, mending, pulsing).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NoHackComponent : Component
{
}

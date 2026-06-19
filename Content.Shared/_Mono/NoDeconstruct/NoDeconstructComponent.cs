using Robust.Shared.GameStates;

namespace Content.Shared._Mono.NoDeconstruct;

/// <summary>
/// Prevents construction/deconstruction interactions when present on an entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NoDeconstructComponent : Component
{
}

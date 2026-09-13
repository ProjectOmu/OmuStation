using Robust.Shared.GameStates;

namespace Content.Shared._Omu.Components;

/// <summary>
/// Used to denote an entity is immune to borer effects
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CorticalBorerProtectionComponent : Component;

using Robust.Shared.GameStates;

namespace Content.Shared._Omu.Traits;

/// <summary>
/// Prevents this entity from entering the Critical mob state.
/// Applied via the NoCrit character trait.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NoCritComponent : Component { }

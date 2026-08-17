namespace Content.Shared._Mono;

/// <summary>
/// Component that applies GodMode to all non-organic entities on a grid.
/// </summary>
[RegisterComponent]
public sealed partial class GridGodModeComponent : Component
{
    /// <summary>
    /// The list of entities that have been given GodMode by this component.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> ProtectedEntities = new();
}

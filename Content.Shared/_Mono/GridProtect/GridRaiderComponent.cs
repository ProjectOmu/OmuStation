namespace Content.Shared._Mono;

/// <summary>
/// Component that applies NoHack and NoDeconstruct to entities with Door and/or VendingMachine components on a grid.
/// Protection is applied once during initialization and remains until the component is removed.
/// </summary>
[RegisterComponent]
public sealed partial class GridRaiderComponent : Component
{
    /// <summary>
    /// The list of entities that have been given NoHack and NoDeconstruct by this component.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> ProtectedEntities = new();

    /// <summary>
    /// Whether to protect entities with Door components.
    /// </summary>
    [DataField]
    public bool ProtectDoors = true;

    /// <summary>
    /// Whether to protect entities with VendingMachine components.
    /// </summary>
    [DataField]
    public bool ProtectVendingMachines = true;

    /// <summary>
    /// Omu: Whether to protect entities with Structure components.
    /// </summary>
    [DataField]
    public bool ProtectStructures = true;
}

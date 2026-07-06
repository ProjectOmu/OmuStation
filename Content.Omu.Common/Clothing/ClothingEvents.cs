namespace Content.Omu.Common.Clothing;

/// <summary>
///     Raised on an entity before a piece of ToggleableClothing is going to place that entity into its container.
/// </summary>
/// <param name="ClothingParent">The parent of the entity to be put into the container. Also the parent of the entity with ToggleableClothing.</param>
[ByRefEvent]
public readonly record struct AboutToEnterToggleableClothingContainerEvent(EntityUid ClothingParent);
namespace Content.Server.Nutrition.Events;

/// <summary>
/// Raised on a food being sliced.
/// Used by deep frier to apply friedness to slices (e.g. deep fried pizza)
/// </summary>
/// <remarks>
/// Not to be confused with upstream SliceFoodEvent which doesn't pass the slice entities, and is only raised once.
/// </remarks>
[ByRefEvent]
public sealed class FoodSlicedEvent(EntityUid user, EntityUid food, EntityUid slice) : EntityEventArgs
{
    /// <summary>
    /// Who did the slicing?
    /// </summary>
    public EntityUid User = user;

    /// <summary>
    /// What has been sliced?
    /// </summary>
    /// <remarks>
    /// This could soon be deleted if there was not enough food left to
    /// continue slicing.
    /// </remarks>
    public EntityUid Food = food;

    /// <summary>
    /// What is the slice?
    /// </summary>
    public EntityUid Slice = slice;
}

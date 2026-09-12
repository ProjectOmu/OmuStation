namespace Content.Shared.Charges.Events;

// The meer fact this has to exist should be a crime.

/// <summary>
///     Raised on the action entity when it is going out of charges on <see cref="Performer">performer</see>.
/// </summary>
/// <param name="Performer">The entity that performed this action.</param>
[ByRefEvent]
public readonly record struct ActionOutOfChargesEvent(EntityUid Performer);

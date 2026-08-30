namespace Content.Shared._Omu.Carrying;

public class CarryAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid Carrier;
    public readonly EntityUid Carried;

    public CarryAttemptEvent(EntityUid carrier, EntityUid carried)
    {
        Carrier = carrier;
        Carried = carried;
    }
}

/// <summary>
///     Raised directed at entity being picked up when someone tries to carry it
/// </summary>
public sealed class GettingCarriedEvent : CarryAttemptEvent
{
    public GettingCarriedEvent(EntityUid carrier, EntityUid carried) : base(carrier, carried) { }
}

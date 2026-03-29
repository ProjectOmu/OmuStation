using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen.Components;


[Serializable, NetSerializable]
public sealed partial class ClearSlagDoAfterEvent : DoAfterEvent
{
    [DataField("solution", required: true)]
    public Solution Solution = default!;

    [DataField("amount", required: true)]
    public FixedPoint2 Amount;

    private ClearSlagDoAfterEvent()
    {
    }

    public ClearSlagDoAfterEvent(Solution solution, FixedPoint2 amount)
    {
        Solution = solution;
        Amount = amount;
    }

    public override DoAfterEvent Clone() => this;
}

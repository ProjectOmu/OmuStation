// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;
using Content.Shared.Chemistry.Components; // GabyStation
using Content.Shared.Inventory; // GabyStation

namespace Content.Shared.Fluids;

[Serializable, NetSerializable]
public sealed partial class AbsorbantDoAfterEvent : DoAfterEvent
{
    [DataField("solution", required: true)]
    public string TargetSolution = default!;

    [DataField("message", required: true)]
    public string Message = default!;

    [DataField("sound", required: true)]
    public SoundSpecifier Sound = default!;

    [DataField("transferAmount", required: true)]
    public FixedPoint2 TransferAmount;

    private AbsorbantDoAfterEvent()
    {
    }

    public AbsorbantDoAfterEvent(string targetSolution, string message, SoundSpecifier sound, FixedPoint2 transferAmount)
    {
        TargetSolution = targetSolution;
        Message = message;
        Sound = sound;
        TransferAmount = transferAmount;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>
/// Raised when trying to spray something, for example a fire extinguisher.
/// </summary>
[ByRefEvent]
public record struct SprayAttemptEvent(EntityUid? User, bool Cancelled = false, string? CancelPopupMessage = null)
{
    public void Cancel()
    {
        Cancelled = true;
    }
}
// GabyStation start
public sealed partial class SpilledOnEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;

    public EntityUid Source;
    public Solution Solution;

    public SpilledOnEvent(EntityUid source, Solution solution)
    {
        Source = source;
        Solution = solution;
    }
}
// GabyStation end

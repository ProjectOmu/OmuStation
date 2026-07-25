using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Vampire.Traits.Positive;

public sealed partial class VampireFangsRetractEvent : InstantActionEvent;

public sealed partial class VampireCharmEvent : InstantActionEvent;

public sealed partial class VampireTransfusionEvent : EntityTargetActionEvent;

public sealed partial class VampireConversionEvent : EntityTargetActionEvent;

public sealed partial class VampireConversionAcceptEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class LesserVampireTransfusionDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class LesserVampireConversionDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}
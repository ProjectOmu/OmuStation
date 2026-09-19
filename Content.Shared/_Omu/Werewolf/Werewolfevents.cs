using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Content.Shared.Polymorph;
using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared.Omu.Werewolf;

public sealed partial class EventWerewolfShiftBasic : InstantActionEvent
{
    [DataField]
    public ProtoId<PolymorphPrototype> Form = "WerewolfBasic";
}

public sealed partial class EventWerewolfRevert : InstantActionEvent
{
}
public sealed partial class EventWerewolfDevour : EntityTargetActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class WerewolfDevourDoAfterEvent : SimpleDoAfterEvent;

using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Content.Shared.Polymorph;

namespace Content.Shared.Omu.Werewolf;

public sealed partial class EventWerewolfShiftBasic : InstantActionEvent
{
    [DataField]
    public ProtoId<PolymorphPrototype> Form = "WerewolfBasic";
}

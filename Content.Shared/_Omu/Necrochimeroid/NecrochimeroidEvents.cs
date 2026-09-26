using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Omu.Entities.Necrochimeroid;

public sealed partial class NecroEnterEvent : EntityTargetActionEvent { }
public sealed partial class NecroLeaveEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed partial class NecroEnterDoafter : SimpleDoAfterEvent
{
    public string Container = string.Empty;
}

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Voidwalker.Actions;

public sealed partial class VoidwalkerUnsettleEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class VoidwalkerUnsettleDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class VoidwalkerKidnapDoAfterEvent : SimpleDoAfterEvent;

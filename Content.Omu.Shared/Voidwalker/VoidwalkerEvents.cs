using Content.Shared.Actions;

namespace Content.Omu.Shared.Voidwalker;

[ByRefEvent]
public record struct VoidwalkerSpacedStatusChangedEvent(bool Spaced);

public sealed partial class VoidWhisperEvent : EntityTargetActionEvent;

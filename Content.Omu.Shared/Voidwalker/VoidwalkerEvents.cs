namespace Content.Omu.Shared.Voidwalker;

[ByRefEvent]
public record struct VoidwalkerSpacedStatusChangedEvent(Entity<VoidwalkerComponent> Entity, bool Spaced);

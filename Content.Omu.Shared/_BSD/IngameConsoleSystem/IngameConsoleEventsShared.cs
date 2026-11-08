using Robust.Shared.Serialization;


namespace Content.Omu.Shared._BSD.IngameConsoleSystem;

[ByRefEvent]
public readonly record struct IngameConsoleCommandCalledEvent(IngameConsoleCommandType Type,
                                                                EntityUid TerminalUid,
                                                                string[]? Args = null //still ships the type with it, aka start reading AFTER index 0
                                                                );//raised on the entity that is effecting it

[Serializable, NetSerializable]
public sealed class IngameConsoleCommandAttemptMessage : BoundUserInterfaceMessage
{
    public string InputString;

    public IngameConsoleCommandAttemptMessage(string requestedAngle)
    {
        InputString = requestedAngle;
    }
}

[ByRefEvent]
public readonly record struct IngameConsoleHistoryChangeEvent(string AddToHistory);

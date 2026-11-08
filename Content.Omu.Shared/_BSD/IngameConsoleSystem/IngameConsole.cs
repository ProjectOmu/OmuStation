using Robust.Shared.Serialization;

namespace Content.Omu.Shared._BSD.IngameConsoleSystem;

[Serializable, NetSerializable]
public enum IngameConsoleUiKey : byte
{
    Key
}
[Serializable, NetSerializable]
public sealed class IngameConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public string[] OutputHistory;

    public IngameConsoleBoundUserInterfaceState(string[] history)
    {
        OutputHistory = history;
    }
}
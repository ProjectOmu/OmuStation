using Content.Omu.Shared._BSD.IngameConsoleSystem;

namespace Content.Omu.Server._BSD.IngameConsoleSystem.Components;

[RegisterComponent]
public sealed partial class IngameConsoleComponent : Component
{
    [DataField]
    public HashSet<IngameConsoleCommandType> AllowedTypes = new();

    [DataField]
    public List<string> History = ["Start"];

    /// <summary>
    /// if the channel permits proxy, null is interpreted as a NO
    /// </summary>
    [DataField]
    public Dictionary<string, bool> PermitsProxy = new();
}
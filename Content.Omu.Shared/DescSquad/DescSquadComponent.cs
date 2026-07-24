using Robust.Shared.GameStates;

namespace Content.Omu.Shared.DescSquad;

[RegisterComponent, NetworkedComponent]
public sealed partial class DescSquadComponent : Component
{
    [DataField]
    public string Color = "#00000000"; // Color of the text,

    [DataField]
    public string Description = ""; // "Her eyes glow * with a vivid wurble"

    [DataField]
    public string Adjective = "vivid"; // "Her eyes glow purple with a * wurble"

    [DataField]
    public string Word = "hatred"; // "Her eyes glow purple with a vivid *"
}

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.DescSquad;

[RegisterComponent, NetworkedComponent]
public sealed partial class DescSquadComponent : Component
{
    [DataField]
    public string Color = "#00000000"; // Color of the text,

    [DataField]
    public string Verb = "glow"; // "Her eyes * purple with a vivid wurble"

    [DataField]
    public string Description = ""; // "Her eyes glow * with a vivid wurble"

    [DataField]
    public string Determiner = "with a"; // "Her eyes glow purple * vivid wurble"

    [DataField]
    public string Adjective = "vivid"; // "Her eyes glow purple with a * wurble"

    [DataField]
    public string Word = "hatred"; // "Her eyes glow purple with a vivid *"

    [DataField]
    public bool IsCustom;

    [DataField]
    public string FullCustom = ""; // blank have fun
}

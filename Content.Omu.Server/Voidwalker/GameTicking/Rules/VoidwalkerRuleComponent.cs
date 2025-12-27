using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Voidwalker.GameTicking.Rules;

[RegisterComponent]
public sealed partial class VoidwalkerRuleComponent : Component
{
    [DataField]
    public SoundPathSpecifier BriefingSound = new("/Audio/_Goobstation/Ambience/Antag/devil_start.ogg"); // Change these

    [ValidatePrototypeId<NpcFactionPrototype>, DataField]
    public string DevilFaction = "DevilFaction"; // Change these

    [ValidatePrototypeId<NpcFactionPrototype>, DataField]
    public string NanotrasenFaction = "NanoTrasen";

    [DataField]
    public EntProtoId DevilMindRole = "DevilMindRole"; // Change these
}

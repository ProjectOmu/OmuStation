using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Voidwalker.GameTicking.Rules;

[RegisterComponent]
public sealed partial class VoidwalkerRuleComponent : Component
{
    [DataField]
    public SoundPathSpecifier BriefingSound = new("/Audio/_Omu/Ambience/Antag/voidwalker_start.ogg");

    [ValidatePrototypeId<NpcFactionPrototype>, DataField]
    public string VoidFaction = "VoidFaction";

    [ValidatePrototypeId<NpcFactionPrototype>, DataField]
    public string NanotrasenFaction = "NanoTrasen";

    [DataField]
    public EntProtoId VoidwalkerMindRole = "VoidwalkerMindRole";
}

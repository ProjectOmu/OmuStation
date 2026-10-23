using Robust.Shared.Prototypes;
using Content.Shared.StatusIcon;
using Content.Shared.NPC.Prototypes;

namespace Content.Omu.Shared.Entities.Heretic;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class FascinationComponent : Component
{
    [DataField]
    public float FascinationValue;

    /// <summary>
    /// A localized description of the current fascination effect.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? ExamineMessage;

    [DataField]
    public LocId MadnessMessage = "fascination-gain";

    [DataField]
    public LocId SanityMessage = "fascination-loss";


    [DataField]
    public int FontSize = 22;

    [DataField]
    public List<LocId> ExamineMessages = new()
    {
        "fascination-examine-1",
        "fascination-examine-2",
        "fascination-examine-3",
        "fascination-examine-4",
        "fascination-examine-5"
    };

    [DataField]
    public bool Naturalsight;

    [DataField]
    public bool NaturalHereticsight;

    [DataField]
    public bool AlteredVision;

    [DataField]
    public ProtoId<FactionIconPrototype> IconToAdd = "MadnessFaction";
    [DataField]
    public ProtoId<NpcFactionPrototype> FactionToAdd = "Madness";

    [DataField]
    public bool AlteredFaction;

}
public sealed class FascinationChangedArgs : EntityEventArgs
{
    public float Amount;
}

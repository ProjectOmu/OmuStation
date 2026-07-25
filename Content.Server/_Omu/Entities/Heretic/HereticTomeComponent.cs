using Content.Server.Heretic.EntitySystems;
using Content.Shared.EntityEffects;
using Robust.Shared.Audio;

namespace Content.Server._Omu.Entities.Heretic;


[RegisterComponent, Access(typeof(HereticTomeSystem))]
public sealed partial class HereticTomeComponent : Component
{
    [DataField]
    public float KnowledgeGain = 1f;

    [DataField]
    public LocId ExamineBaseMessage = "influence-base-message";

    [DataField]
    public int FontSize = 22;

    [DataField]
    public List<LocId> HeathenExamineMessages = new()
    {
        "fracture-examine-message-1",
        "fracture-examine-message-2",
        "fracture-examine-message-3",
        "fracture-examine-message-4",
        "fracture-examine-message-5",
        "fracture-examine-message-6",
        "fracture-examine-message-7",
        "fracture-examine-message-7",
        "fracture-examine-message-8",
        "fracture-examine-message-9",
        "fracture-examine-message-10",
        "fracture-examine-message-11",
        "fracture-examine-message-12",
        "fracture-examine-message-13",
        "fracture-examine-message-14",
        "fracture-examine-message-15",
        "fracture-examine-message-16",
    };

    public List<EntityUid> Readers = new();     //UID's of people who read the book
}

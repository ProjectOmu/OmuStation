namespace Content.Server._Omu.Entities.Heretic;


[RegisterComponent, Access(typeof(FascinationSystem))]
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
}

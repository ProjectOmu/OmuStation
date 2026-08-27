namespace Content.Server._Omu.Revs;

[RegisterComponent, Access(typeof(MoraleSystem))]
public sealed partial class MoraleComponent : Component
{
    [DataField]
    public float MoraleValue = 10f;

    [DataField]
    public int FontSize = 22;

    [DataField]
    public List<LocId> MoraleWarningMsg = new()
    {
        "fracture-examine-message-1"
    };
    [DataField]
    public bool Mindshielded = false;

    [DataField]
    public float MoraleRecovery = 0.2f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float UpdateAccumulator = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float UpdateTimer = 1f;
}

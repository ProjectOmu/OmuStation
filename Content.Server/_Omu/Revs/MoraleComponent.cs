using Content.Shared.NPC.Prototypes;
using Content.Shared.StatusIcon;

namespace Content.Server._Omu.Revs;

[RegisterComponent, Access(typeof(MoraleSystem))]
public sealed partial class MoraleComponent : Component
{
    [DataField]
    public float MoraleValue = 10f;

    [DataField]
    public int FontSize = 22;

    [DataField]
    public string MoraleExamine = "morale-in-question";

    [DataField]
    public List<LocId> MoraleWarningMsg = new()
    {
        "morale-falling-1",
        "morale-falling-2",
        "morale-falling-3"
    };
    [DataField]
    public bool Mindshielded = false;

    [DataField]
    public float MoraleRecovery = 0.2f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float UpdateAccumulator = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float UpdateTimer = 1f;

    [DataField]
    public float MoraleMSRecovery = 1f;

    [DataField]
    public float MoraleMsgSetpoint = 3f;

    [DataField]
    public float MoraleMsgTicking = 0f;
}

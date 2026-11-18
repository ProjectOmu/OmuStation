namespace Content.Server._Omu.Revs;

[RegisterComponent, Access(typeof(MoraleHarmerAreaSystem))]
public sealed partial class MoraleHarmerAreaComponent : Component
{
    [DataField]
    public float MoraleChange = 0f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float UpdateAccumulator = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float UpdateTimer = 1f;

    public float Range = 4f;
}

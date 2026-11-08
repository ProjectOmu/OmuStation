namespace Content.Omu.Server._BSD.SignalSalv.Components;

[RegisterComponent]
public sealed partial class SignalSalvMaterialMiningComponent : Component
{
    /// <summary>
    /// Advanced resources need Outpost data or signal SCI planets
    /// </summary>
    [DataField]
    public bool AdvancedResource = false;

    /// <summary>
    /// Advanced resources need signal SCI planets
    /// </summary>
    [DataField]
    public bool SpecialResource = false;

    /// <summary>
    /// Amount in /100 per second || aka 1 -> 0.01/s 100 is 1/s
    /// </summary>
    [DataField]
    public int MinResoucePerSecond = 1;

    /// <summary>
    /// Amount in /100 per second || aka 1 -> 0.01/s 100 is 1/s
    /// </summary>
    [DataField]
    public int MaxResoucePerSecond = 50;

}

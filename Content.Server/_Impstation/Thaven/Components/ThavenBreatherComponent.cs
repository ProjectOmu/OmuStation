using Content.Server._Impstation.Thaven.Systems;
using Content.Shared.Atmos;

namespace Content.Server._Impstation.Thaven.Components;

[RegisterComponent]
[Access(typeof(ThavenBreatherSystem))]
public sealed partial class ThavenBreatherComponent : Component
{
    [DataField]
    public float MinPressure = Atmospherics.HazardLowPressure;

    [DataField]
    public float SaturationPerBreath = 5f;

    [DataField]
    public Gas IntoxicatingGas = Gas.Frezon;

    [DataField]
    public float IntoxicatingGasMinRatio = 0.1f;

    [DataField]
    public float IntoxicatingGasDurationScale = 6f;

    [DataField]
    public float IntoxicatingGasMinDuration = 0.5f;

    /// <summary>
    /// Optional local cap for drunkenness applied per breath.
    /// Set to 0 or less to rely only on the global drunk system cap.
    /// </summary>
    [DataField]
    public float IntoxicatingGasMaxDuration;

    [DataField]
    public bool IntoxicatingGasApplySlur;
}


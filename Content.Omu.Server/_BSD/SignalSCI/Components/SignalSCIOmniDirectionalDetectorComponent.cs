using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.SignalSCI.Components;

[RegisterComponent]

public sealed partial class SignalSciOmniDirectonalDetectorComponent : Component
{
    /// <summary>
    /// Error margine in pi radia
    /// </summary>
    [DataField]
    public float ErrorMargineBase = MathF.PI * (1.0f / 6.0f);

    [DataField]
    public float ErrorMargineCurrent = MathF.PI * (1.0f / 6.0f);

    [DataField]
    public Dictionary<SignalSciOmniDirectonalDetectorOperationMode, float> ErrorMagineAmplification = new Dictionary<SignalSciOmniDirectonalDetectorOperationMode, float>
    {
        {SignalSciOmniDirectonalDetectorOperationMode.Standard, 1},
        {SignalSciOmniDirectonalDetectorOperationMode.Enhanced, 1},
        {SignalSciOmniDirectonalDetectorOperationMode.Bluespace, 0},
    };

    [DataField]
    public string[] ErrorMargineImprovmentStructure = ["Productivity"];

    [DataField]
    public string[] OperationModeStructure = ["OmniDirectionalDetectorSensor"];

    /// <summary>
    /// Paper that will spawn when printing transcript
    /// </summary>
    [DataField]
    public EntProtoId PaperPrototype = "PaperArtifactAnalyzer";
}

[RegisterComponent]

public sealed partial class SignalSciOmniDirectonalDetectorSensorComponent : Component
{
    [DataField]
    public int SupportedOperationMode = (int) SignalSciOmniDirectonalDetectorOperationMode.Standard;
}

public enum SignalSciOmniDirectonalDetectorOperationMode : int
{
    Unoperable = 0,
    Standard = 1,
    Enhanced = 2,
    Bluespace = 3,
}
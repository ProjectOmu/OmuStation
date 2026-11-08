namespace Content.Omu.Server._BSD.SignalSCI.Components;

[RegisterComponent]

public sealed partial class SignalMapComponent : Component
{
    /// <summary>
    /// Saves the Data needed for the Signals position
    /// </summary>
    [DataField]
    public Dictionary<SignalSciOmniDirectonalDetectorOperationMode, List<Signal>> SignalList = new();

    /// <summary>
    /// how many signals should be active at the same time, multiplied by layer difficulty
    /// </summary>
    [DataField]
    public int[] DesiredAmountOfSignalsPerTierBase = { 2, 7, 1 };

    /// <summary>
    /// Randomised each time a new signal needs to be added
    /// </summary>
    [DataField]
    public int SignalAmountVariance = 2;

    //The time distribution is linear form the bottom to the top time
    /// <summary>
    /// SignalDisapreance Min in seconds
    /// </summary>
    [DataField]
    public float SignalDurationMin = 60 * 5;
    /// <summary>
    /// SignalDisapreance Max in seconds
    /// </summary>
    [DataField]
    public float SignalDurationMax = 60 * 10;
    /// <summary>
    /// SignalPoints Min
    /// </summary>
    [DataField]
    public int SingalPointsMin = 50000;//makes it require upgraded systems to fully harvest a signal
    /// <summary>
    /// SignalPoints Max
    /// </summary>
    [DataField]
    public int SingalPointsMax = 100000;
}

public sealed class Signal
{
    public List<float> Angles;//in pi radia
    public SignalSciOmniDirectonalDetectorOperationMode SignalType;
    public Dictionary<string, int> RemainingData;
    public Dictionary<string, int> DataHarvestingRatio;
    public float EventChanse = 0.05f;//the percentage chanse that a event is triggered upon signal depletion
    public TimeSpan SignalDisaperance;
    public int HintRandomSeed = 0;
    public bool TimeBasedRemoval;
    public bool UnlimitedData;
    public Signal(List<float> angles,
        SignalSciOmniDirectonalDetectorOperationMode signalType,
        Dictionary<string, int> presentData,
        Dictionary<string, int> dataHarvestingRatio,
        TimeSpan signalDisaperance,
        int hintRandomSeed,
        bool timeBasedRemoval = true,
        bool unlimitedData = false)
    {
        Angles = angles;
        SignalType = signalType;
        RemainingData = presentData;
        DataHarvestingRatio = dataHarvestingRatio;
        SignalDisaperance = signalDisaperance;
        HintRandomSeed = hintRandomSeed;
        TimeBasedRemoval = timeBasedRemoval;
        UnlimitedData = unlimitedData;
    }
}


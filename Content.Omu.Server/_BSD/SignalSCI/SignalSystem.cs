using Content.Server.Station.Components;


using Content.Omu.Server._BSD.SignalSCI.Events;
using Content.Omu.Server._BSD.SignalSCI.Components;

using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Omu.Server._BSD.SignalSCI;

/// <summary>
/// This system handles the signal dish multiblock behaviour
/// </summary>
public sealed partial class SignalMapSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignalMapComponent, ComponentStartup>(OnCompInit);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var mapQuerry = AllEntityQuery<SignalMapComponent>();
        while (mapQuerry.MoveNext(out var mapEnt, out var comp))//this is done on update as we also manage the individual signals expration time here
        {
            int counter = -1;
            foreach (var iterator in comp.SignalList.Keys)
            {
                counter++;
                //update the signals aka delete if there time has come
                if (comp.SignalList[iterator].Count > 0)
                {
                    foreach (var signal in comp.SignalList[iterator])
                    {
                        if (signal == null) continue;
                        if (!signal.TimeBasedRemoval) continue;
                        if (signal.SignalDisaperance < _gameTiming.RealTime) comp.SignalList[iterator].Remove(signal);
                    }
                }
                //add more singals if need be, this will lead to high and low times for signal amounts.
                if (comp.SignalList[iterator].Count - comp.DesiredAmountOfSignalsPerTierBase[counter] < comp.SignalAmountVariance)
                {
                    while (comp.SignalList[iterator].Count < comp.DesiredAmountOfSignalsPerTierBase[counter]) CreateSignal(iterator, comp);
                    int additional = _random.Next(0, comp.SignalAmountVariance);
                    for (int i = 0; i < additional; i++) CreateSignal(iterator, comp);
                }
            }
        }
    }
    public void OnCompInit(Entity<SignalMapComponent> ent, ref ComponentStartup args)
    {
        CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Standard, ent.Comp);
        CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Enhanced, ent.Comp);
        CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Bluespace, ent.Comp);
        while (ent.Comp.SignalList[SignalSciOmniDirectonalDetectorOperationMode.Standard].Count < ent.Comp.DesiredAmountOfSignalsPerTierBase[0]) CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Standard, ent.Comp);
        int additional = _random.Next(0, ent.Comp.SignalAmountVariance);
        for (int i = 0; i < additional; i++) CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Standard, ent.Comp);

        while (ent.Comp.SignalList[SignalSciOmniDirectonalDetectorOperationMode.Enhanced].Count < ent.Comp.DesiredAmountOfSignalsPerTierBase[1]) CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Enhanced, ent.Comp);
        additional = _random.Next(0, ent.Comp.SignalAmountVariance);
        for (int i = 0; i < additional; i++) CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Enhanced, ent.Comp);

        while (ent.Comp.SignalList[SignalSciOmniDirectonalDetectorOperationMode.Bluespace].Count < ent.Comp.DesiredAmountOfSignalsPerTierBase[2]) CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Bluespace, ent.Comp);
        additional = _random.Next(0, ent.Comp.SignalAmountVariance);
        for (int i = 0; i < additional; i++) CreateSignal(SignalSciOmniDirectonalDetectorOperationMode.Bluespace, ent.Comp);
    }
    public SignalMapComponent SetupMapSignals(EntityUid uid)//this is called in case the map lacks the component
    {
        EnsureComp<SignalMapComponent>(uid);//ensure the map of the station has signals
        TryComp<SignalMapComponent>(uid, out var comp);
        return comp!;
    }
    public void CreateSignal(SignalSciOmniDirectonalDetectorOperationMode type, SignalMapComponent signalMapComp)
    {
        switch (type)
        {
            case SignalSciOmniDirectonalDetectorOperationMode.Standard:
                CreateSignalTier1(signalMapComp);
                return;
            case SignalSciOmniDirectonalDetectorOperationMode.Enhanced:
                CreateSignalTier2(signalMapComp);
                return;
            case SignalSciOmniDirectonalDetectorOperationMode.Bluespace:
                CreateSignalTier3(signalMapComp);
                return;
        }
        return;
    }
    public void CreateSignalTier1(SignalMapComponent signalMapComp)
    {
        TimeSpan disaperanceTime = TimeSpan.FromMinutes(_random.NextFloat(signalMapComp.SignalDurationMin, signalMapComp.SignalDurationMax)) + _gameTiming.RealTime;
        //MAgic numbers, for the degrees any higher or lower makes no SENCE!!! oddly enought would not braek anything
        List<float> angleList = new();
        angleList.Add(_random.NextFloat(0.0f, (float) Math.PI * 2.0f));
        angleList.Add(0f);
        angleList.Add(0f);
        Dictionary<string, int> dataPresent = new();
        dataPresent.Add("RawTelemetry", _random.Next(signalMapComp.SingalPointsMin, signalMapComp.SingalPointsMax));
        Dictionary<string, int> dataHarvestingRatio = new();
        dataHarvestingRatio.Add("RawTelemetry", 1);
        Signal newSignal = new Signal(angleList, SignalSciOmniDirectonalDetectorOperationMode.Standard, dataPresent, dataHarvestingRatio, disaperanceTime, _random.Next());
        if (signalMapComp.SignalList.ContainsKey(SignalSciOmniDirectonalDetectorOperationMode.Standard) == false) signalMapComp.SignalList.Add(SignalSciOmniDirectonalDetectorOperationMode.Standard, new());
        signalMapComp.SignalList[SignalSciOmniDirectonalDetectorOperationMode.Standard].Add(newSignal);
        return;
    }
    public void CreateSignalTier2(SignalMapComponent signalMapComp)
    {
        TimeSpan disaperanceTime = TimeSpan.FromMinutes(_random.NextFloat(signalMapComp.SignalDurationMin, signalMapComp.SignalDurationMax)) + _gameTiming.RealTime;
        //MAgic numbers, for the degrees any higher or lower makes no SENCE!!! oddly enought would not braek anything
        List<float> angleList = new();
        angleList.Add(_random.NextFloat(0.0f, (float) Math.PI * 2.0f));
        angleList.Add(_random.NextFloat(0.0f, (float) Math.PI * 2.0f));
        angleList.Add(0f);
        Dictionary<string, int> dataPresent = new();
        dataPresent.Add("RawTelemetry", _random.Next(signalMapComp.SingalPointsMin, signalMapComp.SingalPointsMax));//Same cap on amount
        Dictionary<string, int> dataHarvestingRatio = new();
        dataHarvestingRatio.Add("RawTelemetry", 2);//better harvesting ratio for higher tier(flat modifier. magic number bad is known modularity costs more rn)
        Signal newSignal = new Signal(angleList, SignalSciOmniDirectonalDetectorOperationMode.Enhanced, dataPresent, dataHarvestingRatio, disaperanceTime, _random.Next());
        if (signalMapComp.SignalList.ContainsKey(SignalSciOmniDirectonalDetectorOperationMode.Enhanced) == false) signalMapComp.SignalList.Add(SignalSciOmniDirectonalDetectorOperationMode.Enhanced, new());
        signalMapComp.SignalList[SignalSciOmniDirectonalDetectorOperationMode.Enhanced].Add(newSignal);
        return;
    }
    public void CreateSignalTier3(SignalMapComponent signalMapComp)
    {
        TimeSpan disaperanceTime = TimeSpan.FromMinutes(_random.NextFloat(signalMapComp.SignalDurationMin, signalMapComp.SignalDurationMax)) + _gameTiming.RealTime;
        //MAgic numbers, for the degrees any higher or lower makes no SENCE!!! oddly enought would not braek anything
        List<float> angleList = new();
        angleList.Add(_random.NextFloat(0.0f, (float) Math.PI * 2.0f));
        angleList.Add(_random.NextFloat(0.0f, (float) Math.PI * 2.0f));
        angleList.Add(_random.NextFloat(0.0f, (float) Math.PI * 2.0f));
        Dictionary<string, int> dataPresent = new();
        dataPresent.Add("RawTelemetry", int.MaxValue);
        dataPresent.Add("RawBluespaceTelemetry", int.MaxValue);
        Dictionary<string, int> dataHarvestingRatio = new();
        dataHarvestingRatio.Add("RawTelemetry", 4);//better harvesting ratio for higher tier(flat modifier. magic number bad is known modularity costs more rn)
        dataHarvestingRatio.Add("RawBluespaceTelemetry", 1);//better harvesting ratio for higher tier(flat modifier. magic number bad is known modularity costs more rn)
        Signal newSignal = new Signal(angleList, SignalSciOmniDirectonalDetectorOperationMode.Bluespace, dataPresent, dataHarvestingRatio, disaperanceTime, _random.Next(), false, true);
        if (signalMapComp.SignalList.ContainsKey(SignalSciOmniDirectonalDetectorOperationMode.Bluespace) == false) signalMapComp.SignalList.Add(SignalSciOmniDirectonalDetectorOperationMode.Bluespace, new());
        signalMapComp.SignalList[SignalSciOmniDirectonalDetectorOperationMode.Bluespace].Add(newSignal);
        return;
    }
}
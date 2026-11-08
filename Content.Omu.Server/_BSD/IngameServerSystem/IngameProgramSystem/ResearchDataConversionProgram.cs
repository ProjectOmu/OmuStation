using Content.Omu.Server._BSD.IngameServerSystem;
using Content.Omu.Server._BSD.IngameServerSystem.Helpers;
using Content.Omu.Server._BSD.IngameServerSystem.Components;
using Content.Omu.Server._BSD.IngameServerSystem.Events;

using Content.Shared.Research.Components;

using Content.Omu.Server._BSD.IngameConsoleSystem.IngameProgramSystem.Components;

namespace Content.Omu.Server._BSD.IngameConsoleSystem.IngameProgramSystem;

public sealed class BSDIngamePointConversionProgramSystem : EntitySystem
{
    //[Dependency] private readonly BSDIngameServerSystem _ingameServerSys = default!;
    public float UpdateFrequencyInSeconds = 0.25f;//needs help how to more easily scny this probably a CVar probably a CVar
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IngameServerComponent, IngameServerProgrammExecutionEvent>(ExecuteProgram);
        SubscribeLocalEvent<IngameServerComponent, ResearchServerGetPointsPerSecondEvent>(TEMPORARYInterfacingWithRndSystem);
    }
    public void ExecuteProgram(Entity<IngameServerComponent> ent, ref IngameServerProgrammExecutionEvent args)
    {
        if (args.Type != IngameServerProgramTypes.ResearchProgram) return;//somehow getting rid of this check not shure how
        if (!TryComp<IngameServerComponent>(ent, out var comp)) return;
        if (!TryComp<IngamePointConversionProgramComponent>(ent, out var programComp))
        {
            programComp = SetupProgramComp(ent);
        }
        if (programComp == null)
        {
            Log.Error("ServerEnt: " + ent + " did not contain the IngamePointConversionProgramComponent but was expected to.");
            return;
        }
        //
        // Converts from raw data to processed
        // conversion maths: f(time) = time * Min(RawData, capacity) * converstion factor * dynamic conversion factor
        //
        IngameServerPointConversions loadedConversion = new();
        foreach (string iterator in programComp.EnabeledConversions)
        {
            if (!loadedConversion.PointAToPointB.ContainsKey(iterator)) continue;
            if (!loadedConversion.ConversionRate.ContainsKey(iterator)) continue;
            if (!comp.StoredPoints.ContainsKey(iterator)) comp.StoredPoints.Add(iterator, 0);
            if (!comp.StoredPoints.ContainsKey(loadedConversion.PointAToPointB[iterator])) comp.StoredPoints.Add(loadedConversion.PointAToPointB[iterator], 0);
            float convertedData = 0f;
            convertedData += Math.Min(comp.StoredPoints[iterator], 300f) * (UpdateFrequencyInSeconds / 1);
            comp.StoredPoints[iterator] -= (int) convertedData;//remove the points we took
            convertedData *= comp.ActivePrograms[IngameServerProgramTypes.ResearchProgram].Efficency;
            convertedData *= loadedConversion.ConversionRate[iterator];
            comp.StoredPoints[loadedConversion.PointAToPointB[iterator]] += (int) convertedData;
        }
        return;
    }
    public IngamePointConversionProgramComponent SetupProgramComp(EntityUid ent)
    {
        EnsureComp<IngamePointConversionProgramComponent>(ent);//ensure the map of the station has signals
        TryComp<IngamePointConversionProgramComponent>(ent, out var comp);
        return comp!;
    }
    #region TEMPORARY
    //dumps points into the RND budget every second
    public void TEMPORARYInterfacingWithRndSystem(Entity<IngameServerComponent> source, ref ResearchServerGetPointsPerSecondEvent args)
    {
        args.Points += source.Comp.StoredPoints["SciGeneralPoint"];
        source.Comp.StoredPoints["SciGeneralPoint"] = 0;
    }

    #endregion
}

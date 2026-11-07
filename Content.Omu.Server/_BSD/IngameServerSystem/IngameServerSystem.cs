using System.Linq;

using Robust.Server.GameObjects;

using Robust.Shared.Timing;

using Content.Omu.Shared._BSD.IngameConsoleSystem;

using Content.Omu.Server._BSD.IngameConsoleSystem;
using Content.Omu.Server._BSD.IngameConsoleSystem.Components;

using Content.Omu.Server._BSD.IngameServerSystem.Helpers;
using Content.Omu.Server._BSD.IngameServerSystem.Components;
using Content.Omu.Server._BSD.IngameServerSystem.Events;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem;
using Content.Omu.Server._BSD.MultiBlockSystem.Events;

namespace Content.Omu.Server._BSD.IngameServerSystem;

public sealed partial class BSDIngameServerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BSDIngameConsoleSystem _consoleSys = default!;
    [Dependency] private readonly BSDIngameServerClientLinkSystem _clientLinkSystem = default!;
    public float UpdateFrequencyInSeconds = 0.25f;//I recomend Programs use this to do math as there tickrate
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IngameServerComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommand);
        SubscribeLocalEvent<IngameServerComponent, ComponentStartup>(OnComponentSetup);
        SubscribeLocalEvent<IngameServerComponent, MultiStructChangeEvent>(OnMultistructUpdate);
        //Point handeling
        SubscribeLocalEvent<IngameServerPointCapacityComponent, MultiStructChangeEvent>(OnMultistructUpdatePointCapacity);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var machineQuerry = AllEntityQuery<IngameServerComponent>();
        while (machineQuerry.MoveNext(out var uidLoop, out var comp))
        {
            if (comp.NextUpdate < _timing.CurTime)
            {
                RunPrograms(uidLoop, comp);
                comp.NextUpdate += TimeSpan.FromSeconds(UpdateFrequencyInSeconds);
            }
        }
    }
    public void OnComponentSetup(EntityUid uid, IngameServerComponent comp, ComponentStartup args)
    {
        IngameProgramList ingameProgramListInstance = new();
        foreach (IngameServerProgram iterator in ingameProgramListInstance.Content)
        {
            if (!comp.InstalledPrograms.Contains((float) iterator.Type)) continue;
            comp.ActivePrograms.Add(iterator.Type, iterator);
        }
    }
    public void OnMultistructUpdate(Entity<IngameServerComponent> ent, ref MultiStructChangeEvent args)
    {
        return;
    }
    public void RunPrograms(EntityUid uid, IngameServerComponent comp)
    {
        foreach (IngameServerProgramTypes iterator in comp.ActivePrograms.Keys)
        {
            IngameServerProgrammExecutionEvent ev = new(iterator, comp.ActivePrograms[iterator].AssignedProcessingCost);
            RaiseLocalEvent(uid, ref ev);
        }
        return;
    }
    #region UserIntrerfacing
    public void IngameConsoleCommand(Entity<IngameServerComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        // assign processing <ID!> <amount!> <override(y/n)?>
        if (args.Type == IngameConsoleCommandType.ICC_ASSIGN && args.Args!.Length > 4 && args.Args[1] == "processing")
        {
            bool overrideAlotmentDesire = false;
            IngameServerProgramTypes programType;
            if (!int.TryParse(args.Args[2], out int helperInt)) return;
            programType = (IngameServerProgramTypes) helperInt;
            float deltaChange = 0;
            if (!float.TryParse(args.Args[3], out deltaChange)) return;
            if (args.Args!.Length > 5)
            {
                overrideAlotmentDesire = _consoleSys.InputBoolCheck(args.Args[4]);
            }
            if (!TryComp<IngameServerComponent>(ent, out var comp)) return;
            ChangeProcessingAlotment(comp, programType, deltaChange, overrideAlotmentDesire);
        }

    }
    #endregion
    #region Processing capacity
    public void ChangeProcessingAlotment(IngameServerComponent comp, IngameServerProgramTypes type, float change, bool overrideAlotment)
    {
        float deltaChange = 0;
        if (!overrideAlotment && comp.AvailabeProcessingPower < change) deltaChange = comp.AvailabeProcessingPower;
        else if (overrideAlotment && comp.AvailabeProcessingPower < change && comp.ActivePrograms[type].Priority < 3)
        {
            deltaChange = OverrideAlotment(comp, type, change, comp.ActivePrograms[type].Priority);
        }
        IndividualProgramAlotmentChange(comp, type, deltaChange);
    }
    private void IndividualProgramAlotmentChange(IngameServerComponent comp, IngameServerProgramTypes type, float deltaChange)
    {
        IngameServerProgram replacmentprogram = new(comp.ActivePrograms[type].Type, comp.ActivePrograms[type].BaseProcessingCost, comp.ActivePrograms[type].Priority);
        replacmentprogram.AssignedProcessingCost += deltaChange;
        comp.ActivePrograms[type] = replacmentprogram;
        comp.AvailabeProcessingPower -= deltaChange;
    }
    //Return the atained difference
    private float OverrideAlotment(IngameServerComponent comp, IngameServerProgramTypes type, float neededDifference, int overridePriority)
    {
        float atainedDeltaChange = 0f;
        List<IngameServerProgram> programmList = new();
        foreach (IngameServerProgramTypes iterator in comp.ActivePrograms.Keys)
        {
            programmList.Add(comp.ActivePrograms[iterator]);
        }
        programmList.Sort((s1, s2) => s1.Priority.CompareTo(s2.Priority));
        int whileIterator = 0;
        while (atainedDeltaChange < neededDifference && programmList[whileIterator].Priority > overridePriority)
        {
            if (neededDifference - atainedDeltaChange < programmList[whileIterator].AssignedProcessingCost)
            {
                IndividualProgramAlotmentChange(comp, programmList[whileIterator].Type, (-neededDifference + atainedDeltaChange));
                return neededDifference;
            }
            atainedDeltaChange += programmList[whileIterator].AssignedProcessingCost;
            IndividualProgramAlotmentChange(comp, programmList[whileIterator].Type, programmList[whileIterator].AssignedProcessingCost);
        }
        return atainedDeltaChange;
    }
    #endregion
}
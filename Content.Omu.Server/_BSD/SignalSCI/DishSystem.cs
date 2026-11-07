
using Robust.Shared.Collections;
using Robust.Shared.Map.Components;
using Robust.Shared.GameObjects;

using Content.Server.Research.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Research;

using Content.Omu.Server._BSD.SignalSCI.Components;
using Content.Omu.Server._BSD.MultiBlockSystem.Events;
using Content.Omu.Server._BSD.MultiBlockSystem.Components;
using Content.Omu.Server._BSD.MultiBlockSystem;

using Content.Omu.Shared._BSD.IngameConsoleSystem;
using Content.Omu.Server._BSD.IngameServerSystem;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;
using System.Linq;


namespace Content.Omu.Server._BSD.SignalSCI;

/// <summary>
/// This system handles the signal dish multiblock behaviour
/// </summary>
public sealed partial class SignalDishSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSys = default!;
    [Dependency] private readonly SharedTransformSystem _trans = default!;
    [Dependency] private readonly SignalMapSystem _signalMap = default!;
    [Dependency] private readonly BSDIngameServerSystem _ingameServerSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignalSciDishComponent, MultiStructChangeEvent>(UpdateValuesMultiStruct);
        SubscribeLocalEvent<SignalSciDishComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommand);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SignalSciDishComponent, MultiBlockEnergyManagmentComponent>();
        while (query.MoveNext(out var dishEnt, out var comp, out var energycomp))
        {
            //if (!energycomp.Powered) continue;//do nothing if no power is in the system -> we ignore power for testing
            if (comp.Harvesting)//basicly if this machine is turned on -> needs to be move to multi struct possibly
            {
                DishSignalHarvest(dishEnt, comp);
            }
            RotationUpdate(dishEnt, comp);
        }
    }
    public void IngameConsoleCommand(Entity<SignalSciDishComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        if (args.Type == IngameConsoleCommandType.ICC_SET && args.Args!.Length > 2)
        {
            IngameConsoleHistoryChangeEvent ev = new(Loc.GetString("SSI_Dish_Orientation_Set", ("Variable", args.Args[1]), ("Value", args.Args[2])));
            RaiseLocalEvent(args.TerminalUid, ref ev);
            UpdateVariableIngameConsoleCommand(args.Args[1], args.Args[2], ent.Comp);
        }
    }
    private void UpdateVariableIngameConsoleCommand(string varID, string value, SignalSciDishComponent comp)
    {
        int intParsRes = 0;//switch does not like local declaration so we doing it this way
        switch (varID)
        {
            case ("rotation_1"):
                if (!int.TryParse(value, out intParsRes)) return;
                comp.DesiredAngles[0] = intParsRes % 360;
                return;
            case ("rotation_2"):
                if (!int.TryParse(value, out intParsRes)) return;
                comp.DesiredAngles[1] = intParsRes % 360;
                return;
            case ("rotation_3"):
                if (!int.TryParse(value, out intParsRes)) return;
                comp.DesiredAngles[2] = intParsRes % 360;
                return;
            default:
                break;
        }
    }
    private void RotationUpdate(EntityUid uid, SignalSciDishComponent comp)
    {
        RotationUpdateUnobservedDimention(uid, comp);
        RotationUpdateObservedDimention(uid, comp);//we only need to do visual changes for one dimention as the game is in 2d and not 3d OR even 4d
        return;
    }
    private void RotationUpdateObservedDimention(EntityUid uid, SignalSciDishComponent comp)
    {
        if (!TryComp<MultiBlockStructureComponent>(uid, out var multistructcomp)) return;//consider making this a proper methode to call
        if (!multistructcomp.EntityDic!.ContainsKey("SignalAntenna")) return;
        EntityUid antennaUid = multistructcomp.EntityDic["SignalAntenna"][0].Id;//gets the first entry
        TransformComponent transcomp = Transform(antennaUid);
        if (transcomp.GridUid == null) return;
        TransformComponent gridTransformComp = Transform(transcomp.GridUid!.Value);
        Angle newAngel = (Angle) comp.CurrentAngles[0] + transcomp.LocalRotation;
        _trans.SetWorldRotation(gridTransformComp, newAngel);
        return;
    }
    private void RotationUpdateUnobservedDimention(EntityUid uid, SignalSciDishComponent comp)
    {
        for (int iterator = 0; iterator < 3; iterator++)
        {
            float angle = comp.CurrentAngles[iterator] * (180.0f / MathF.PI);
            float maxRotation = 0f;
            if (angle <= comp.AngleErrorMargine + comp.DesiredAngles[iterator] && angle >= comp.DesiredAngles[iterator] - comp.AngleErrorMargine)
            {
                continue;
            }
            maxRotation = comp.DesiredAngles[iterator] - angle;
            if (maxRotation > 360.0f + angle - comp.DesiredAngles[iterator]) maxRotation = 360.0f + angle - comp.DesiredAngles[iterator];
            maxRotation = MathF.Max(MathF.Min(maxRotation, comp.MaxRotationSpeed), -1 * comp.MaxRotationSpeed) * (float) (Math.PI / 180.0f);
            Angle newAngel = (Angle) (comp.CurrentAngles[iterator] + maxRotation);
            comp.CurrentAngles[iterator] = (float) newAngel;
        }
    }
    private void UpdateValuesMultiStruct(EntityUid uid, SignalSciDishComponent comp, ref MultiStructChangeEvent args)
    {
        if (!TryComp<MultiBlockStructureComponent>(uid, out var structureComp)) return;
        //harvesting rate
        comp.HarvestingRate = 0f;
        foreach (string providerType in comp.DishTypes)
        {
            if (!structureComp.TypesPresent.ContainsKey(providerType)) continue;
            comp.HarvestingRate += structureComp.TypesPresent[providerType] * comp.HarvestingBaseRate;
        }
        //Conversion efficency
        comp.EfficencyConversion = comp.EfficencyBase;
        foreach (string providerType in comp.ProductivityTypes)
        {
            if (!structureComp.TypesPresent.ContainsKey(providerType)) continue;
            comp.EfficencyConversion += structureComp.TypesPresent[providerType];//productivity modules always have efficency rating coresponding to there buff!!!
        }
        return;
    }

    private void DishSignalHarvest(EntityUid uid, SignalSciDishComponent dishComp)
    {
        EntityUid mapUid = _mapSys.GetMapOrInvalid(Transform(uid).MapID);
        if (!TryComp<IngameServerClientLinkInfrastructureComponent>(uid, out var infraComp)) return;
        if (!infraComp.EntityDicServer.ContainsKey("ResearchPoints")) return;//TODO: possibly make this modular and not hard coded!!!!
        EntityUid linkedServer = infraComp.EntityDicServer["ResearchPoints"].First();//there should only be one anyway
        if (mapUid == EntityUid.Invalid) return;
        if (!TryComp<SignalMapComponent>(mapUid, out var comp))
        {
            comp = _signalMap.SetupMapSignals(mapUid);
        }
        if (comp == null)
        {
            Log.Error("MapEnt: " + mapUid + " did not contain the SignalMapComponent but was expected to.");
            return;
        }
        dishComp.HarvestingRate = 0f;
        //Treat every signal as if it is on the same 4d unit sphere lower tier signals simply only have 1 or 2 things that arent set to 0 degrees
        for (SignalSciOmniDirectonalDetectorOperationMode mode = SignalSciOmniDirectonalDetectorOperationMode.Standard; mode <= SignalSciOmniDirectonalDetectorOperationMode.Bluespace; mode++)
        {
            foreach (var iterator in comp.SignalList[mode])
            {
                //range 0 to 1
                float allignment = MathF.Pow(CalculateAlignment(dishComp, iterator), 3f);
                float harvestingRate = dishComp.HarvestingBaseRate * allignment;
                dishComp.HarvestingRate += harvestingRate; //this is a debugging number and could be removed TODO: remove this once everything works
                foreach (var signalData in iterator.RemainingData.Keys)
                {
                    _ingameServerSystem.TryAddMaxPoints(linkedServer, signalData, (int) MathF.Min(MathF.Round(harvestingRate), iterator.RemainingData[signalData]));
                    if (iterator.UnlimitedData) continue;
                    iterator.RemainingData[signalData] -= (int) MathF.Min(MathF.Round(harvestingRate), iterator.RemainingData[signalData]);
                }
            }
        }
        return;
    }

    private float CalculateAlignment(SignalSciDishComponent comp, Signal signal)
    {
        double[] vectorA = new double[4];
        double[] vectorB = new double[4];
        // converting 4D hyperspherical coordinates (r, nu, theta, phi) to Cartesian coordinates (x, y, z, w)
        var cosNu = Math.Cos(signal.Angles[0]);
        var sinNu = Math.Sin(signal.Angles[0]);
        var cosTheta = Math.Cos(signal.Angles[1]);
        var sinTheta = Math.Sin(signal.Angles[1]);
        var cosPhi = Math.Cos(signal.Angles[2]);
        var sinPhi = Math.Sin(signal.Angles[2]);
        // since r=1, we can ignore it in multiplication
        vectorA[0] = cosNu * cosTheta * cosPhi;
        vectorA[1] = cosNu * cosTheta * sinPhi;
        vectorA[2] = cosNu * sinTheta;
        vectorA[3] = sinNu;
        cosNu = Math.Cos(comp.CurrentAngles[0]);
        sinNu = Math.Sin(comp.CurrentAngles[0]);
        cosTheta = Math.Cos(comp.CurrentAngles[1]);
        sinTheta = Math.Sin(comp.CurrentAngles[1]);
        cosPhi = Math.Cos(comp.CurrentAngles[2]);
        sinPhi = Math.Sin(comp.CurrentAngles[2]);
        vectorB[0] = cosNu * cosTheta * cosPhi;
        vectorB[1] = cosNu * cosTheta * sinPhi;
        vectorB[2] = cosNu * sinTheta;
        vectorB[3] = sinNu;
        double dotproductAB = 0.0;
        dotproductAB += vectorA[0] * vectorB[0];
        dotproductAB += vectorA[1] * vectorB[1];
        dotproductAB += vectorA[2] * vectorB[2];
        dotproductAB += vectorA[3] * vectorB[3];
        return Math.Max(0, (float) dotproductAB);
    }
}
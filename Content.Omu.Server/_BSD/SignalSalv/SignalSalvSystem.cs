
using Content.Omu.Shared._BSD.IngameConsoleSystem;

using Content.Omu.Server._BSD.SignalSalv.Components;
using Content.Omu.Server._BSD.SignalSalv.Events;
using Content.Omu.Server._BSD.SignalSalv.Helpers;

using Content.Omu.Server._BSD.MultiBlockSystem.Events;
using Content.Omu.Server._BSD.MultiBlockSystem.Components;

using Content.Omu.Server._BSD.IngameServerSystem;

using Content.Omu.Server._BSD.IngameConsoleSystem;

using Robust.Shared.Timing;

using System.Linq;
using System.Numerics;


using Content.Shared.Materials;
using Content.Shared.Interaction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Atmos;

using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Utility;
using Robust.Shared.Physics.Components;

using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Events;
using Content.Shared.Shuttles.Systems;

namespace Content.Omu.Server._BSD.SignalSalv;

public sealed partial class BSDSignalSalvSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedMapSystem _mapSys = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly BSDIngameServerSystem _ingameServer = default!;
    [Dependency] private readonly BSDSignalSalvMaterialTransitSystem _materialTransitSystem = default!;

    public static readonly CompleteMaterialList MaterialMiningRatesBase = new();
    private static readonly ProtoId<BiomeTemplatePrototype> BiomeTemplate = "Continental";
    public const string PlanetGenerationSettingsDiskSlotName = "PlanetGenerationSettingsDiskSlotName";
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignalSalvFtlDeviceComponent, ComponentInit>(OnInitFTLDrive);

        SubscribeLocalEvent<SignalSalvMaterialReciverStructureComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommandMatReciver);

        SubscribeLocalEvent<SignalSalvFtlDeviceComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommandSignalSalvFTLDevice);
        SubscribeLocalEvent<SignalSalvFtlDeviceBasedFTLComponent, FTLCompletedEvent>(DeleteLinkedMap);

        SubscribeLocalEvent<SignalSalvMiningRigStructreComponent, IngameConsoleCommandCalledEvent>(IngameConsoleCommandSignalSalvMiningRig);
        SubscribeLocalEvent<SignalSalvMiningRigStructreComponent, MultiStructChangeEvent>(MiningRigRecalculationStructureChange);

        SubscribeLocalEvent<SignalSalvOutpostDataComponent, AfterInteractEvent>(OnAfterInteractOutpostData);

    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var queryFTLDrives = EntityQueryEnumerator<SignalSalvFtlDeviceComponent>();
        while (queryFTLDrives.MoveNext(out var driveEnt, out var drivecomp))
        {
            ChargeFTLCapacitiors(driveEnt, drivecomp);
        }
    }
    #region Inicialisation
    private void OnInitFTLDrive(EntityUid uid, SignalSalvFtlDeviceComponent comp, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, PlanetGenerationSettingsDiskSlotName, comp.GenerationSettingsDiskSlot);

    }
    #endregion
    #region User Interfacing
    public void IngameConsoleCommandSignalSalvFTLDevice(Entity<SignalSalvFtlDeviceComponent> ent, ref IngameConsoleCommandCalledEvent args)
    {
        if (args.Type == IngameConsoleCommandType.SSA_FTL && args.Args!.Length > 1 && args.Args[1] == "jump")
        {
            IngameConsoleHistoryChangeEvent ev = new("-> FTL Attempt started");
            RaiseLocalEvent(args.TerminalUid, ref ev);
            GenerateExpeditionMapAndFTL(ent, ent.Comp);
        }
        else if (args.Type == IngameConsoleCommandType.SSA_FTL && args.Args!.Length > 1 && args.Args[1] == "calculate")
        {
            IngameConsoleHistoryChangeEvent ev = new("-> FTL Coordinates calculated");
            RaiseLocalEvent(args.TerminalUid, ref ev);
            GenerateJumpPoint(ent.Comp);
        }
        else if (args.Type == IngameConsoleCommandType.SSA_FTL && args.Args!.Length > 1 && args.Args[1] == "return")
        {
            IngameConsoleHistoryChangeEvent ev = new("-> Returning to station");
            RaiseLocalEvent(args.TerminalUid, ref ev);
            ReturnToStationSceduleDeleteMap(ent, ent.Comp);
        }
        else if (args.Type == IngameConsoleCommandType.ICC_PRINT && args.Args!.Length > 1 && args.Args[1] == "ftl")
        {
            IngameConsoleHistoryChangeEvent ev = new(PrintFTLStatus(ent));
            RaiseLocalEvent(args.TerminalUid, ref ev);
        }
    }
    //TODO move this function into the MiningMaterialTransitSystem
    private string PrintMaterialInbound(EntityUid uidReciver)
    {
        if (!TryComp<SignalSalvMaterialReciverStructureComponent>(uidReciver, out var compReciver)) return "ERROR- THIS IS NOT A MATERIAL RECIVER";
        string returnString = "";
        if (_materialTransitSystem.MaterialInTransit.Keys == null) return returnString;
        foreach (var iterator in _materialTransitSystem.MaterialInTransit.Keys)//TEMP REPLACE WITH PROPER LOCALISATION LATER!!!!
        {
            string subAddition = "";
            subAddition += (string) iterator;
            subAddition += " -> stored off site:";
            subAddition += _materialTransitSystem.MaterialInTransit[iterator].ToString();
            subAddition += " | next delivery at Offsite storage of: ";
            subAddition += compReciver.MaterialCargoMin.ToString();
            if (_materialTransitSystem.MaterialProductionPerSecond.ContainsKey(iterator))
            {
                subAddition += " | Offsite mining rate: ";
                subAddition += _materialTransitSystem.MaterialProductionPerSecond[iterator].ToString();
            }
            else
            {
                subAddition += " | No offsite mining detected";
            }
            subAddition += "\n(";
            int counter = 1;
            while (counter < 11)
            {
                if (_materialTransitSystem.MaterialInTransit[iterator] >= (_materialTransitSystem.MaterialProductionPerSecond[iterator] / (counter / 10.0f)))
                {
                    subAddition += "|";
                }
                else
                {
                    subAddition += "-";
                }
                counter++;
            }
            subAddition += ")\n";
            returnString += subAddition;
        }
        /*
        The above code creats something akin to this:
        Material -> stored([Amount in transit])|delivery at([Amount needed to arrive])|production([Productionrate]/s)
        Material (Progress bar)
        example:
        Stell -> stored(10)|delivery at(20)|production(0.1/s)
        Steel [|||||-----]
        Glass -> stored(15)|delivery at(20)|production(0.15/s)
        Glass [|||||||---]
        */
        return returnString;
    }
    private string PrintFTLStatus(EntityUid uidReciver)
    {
        EntityUid mapUid = _mapSys.GetMapOrInvalid(Transform(uidReciver).MapID);
        if (!TryComp<SignalSalvFtlDeviceComponent>(uidReciver, out var compFTL)) return "ERROR- THIS IS NOT A FTL DRIVE";
        TransformComponent transComp = Transform(uidReciver);
        if (transComp.GridUid! == null) return "ERROR - NO SHUTTLE ATTACHED";
        var gridUid = transComp.GridUid.Value;
        if (!TryComp<PhysicsComponent>(gridUid, out var physicsComp)) return "ERROR- THE SHUTTLE<" + gridUid + "> DOES NOT HAVE PHYSICS";
        Vector2d vector = new(transComp.Coordinates.X, transComp.Coordinates.Y);
        string returnString = "";
        //Drive charge
        returnString += "-> FTL Capacitor Charge: " + compFTL.FTLCapacitiorsStoredCharge + " out of required: " + compFTL.FTLCharge;
        returnString += "\n(";
        int counter = 1;
        while (counter < 11)
        {
            if (compFTL.FTLCapacitiorsStoredCharge >= (compFTL.FTLCharge * (counter / 10.0f)))
            {
                returnString += "|";
                if (counter == 10)
                {
                    returnString += ") -> (FTL DRIVE CHARGED";
                }
            }
            else
            {
                returnString += "-";
            }
            counter++;
        }
        //Jump point
        returnString += ")\n";
        if (compFTL.JumpPointSet)
        {
            returnString += "-> Jump Point at location: (" + compFTL.DesignatedJumpPoint.X + "|" + compFTL.DesignatedJumpPoint.Y + ")\n";
            returnString += "-> Approximate Jump Point distance: " + GetDistance(vector, compFTL.DesignatedJumpPoint) + "\n";
            var distance = GetDistance(vector, compFTL.DesignatedJumpPoint);
            if (distance <= compFTL.JumpPointTolerance)
                returnString += "-> (SHIP IN POSITION)\n";
        }
        else
        {
            returnString += "-> Jump point is yet to be calculated\n";
        }
        returnString += "-> FTL Drive Mass Capacity: " + compFTL.MaxFTLGridMass + " Current shuttle mass: " + physicsComp!.Mass;
        returnString += "\n(";
        counter = 1;
        while (counter < 11)
        {
            if (physicsComp!.Mass <= (compFTL.MaxFTLGridMass * (10 - counter / 10.0f)))
            {
                returnString += "|";
                if (counter == 10)
                {
                    returnString += ") -> (FTL DRIVE STRONG ENOUGHT";
                }
            }
            else
            {
                returnString += "-";
            }
            counter++;
        }
        returnString += ")\n";
        return returnString;
    }
    #endregion
    #region Generate Map and travel systems
    public void ChargeFTLCapacitiors(EntityUid uid, SignalSalvFtlDeviceComponent ftlComp)
    {
        float deltaTime = (float) _timing.CurTime.TotalMilliseconds - (float) ftlComp.LastUpdate.TotalMilliseconds;
        ftlComp.LastUpdate = _timing.CurTime;
        if (!TryComp<MultiBlockEnergyManagmentComponent>(uid, out var energyComp)) return;
        Int64 chargeRate = ftlComp.FTLCapacitiorChargeRate;
        if (energyComp.StoredEnergy < (chargeRate * (deltaTime / 1000.0f)))
        {
            chargeRate = Math.Max((Int64) energyComp.StoredEnergy, 0);//in case we SOMEHOW get negative energy
        }
        if (chargeRate * (ftlComp.FTLCapacitiorChargeEfficency / 100.0f) + ftlComp.FTLCapacitiorsStoredCharge >= ftlComp.FTLCharge)
        {
            chargeRate = (Int64) ((ftlComp.FTLCharge - ftlComp.FTLCapacitiorsStoredCharge) / (ftlComp.FTLCapacitiorChargeEfficency / 100.0f));
        }
        ftlComp.FTLCapacitiorsStoredCharge += Math.Max((Int64) (chargeRate * (ftlComp.FTLCapacitiorChargeEfficency / 100.0f)), 0);//FTL discharges otherwise
        energyComp.StoredEnergy -= (Int64) chargeRate;
    }
    public void GenerateExpeditionMapAndFTL(EntityUid shuttleConsole, SignalSalvFtlDeviceComponent ftlComp)
    {
        TransformComponent transComp = Transform(shuttleConsole);
        if (transComp.MapUid == ftlComp.CurrentlyLinkedMapUid)
        {
            IngameConsoleHistoryChangeEvent ev = new("-> FTL CHANCELLED | Ship is already on expedition planet");
            RaiseLocalEvent(shuttleConsole, ref ev);
            return;
        }
        if (!FTLChecklist(transComp, shuttleConsole, ftlComp, out var shuttleComponent)) return;
        //Move the ship
        var tagEv = new FTLTagEvent();
        RaiseLocalEvent(transComp.GridUid!.Value, ref tagEv);
        var targetCoordinates = new EntityCoordinates((EntityUid) ftlComp.CurrentlyLinkedMapUid!, new Vector2(0, 0));
        Angle targetAngle = new();
        ftlComp.Originmap = transComp.MapUid;
        PostFTLCost(shuttleConsole, transComp.GridUid.Value, ftlComp);
        _shuttle.FTLToCoordinates(transComp.GridUid.Value, shuttleComponent!, targetCoordinates, targetAngle);
        return;
    }
    private void PostFTLCost(EntityUid consoleUid, EntityUid gridUid, SignalSalvFtlDeviceComponent comp)
    {
        comp.FTLCapacitiorsStoredCharge -= comp.FTLCharge;
        comp.JumpPointSet = false;
        EnsureComp<SignalSalvFtlDeviceBasedFTLComponent>(gridUid, out var compFTL);
        compFTL.LinkedFTLDevice = consoleUid;
        RemCompDeferred<FTLComponent>(gridUid);//Bypasses the FTL cooldown
        if (!TryComp<FTLComponent>(gridUid, out var ftlComp)) return;
        ftlComp.State = FTLState.Available;//it is deleted and readded NEXT tick but we need to jump
    }
    /// <summary>
    /// Ensure that the FTL drive is:
    /// - anchored
    /// - has a grid
    /// - the grid is a shuttle
    /// - the grid has enought FTL strength
    /// - ensure we have a linked map
    /// </summary>
    /// <param name="transComp"></param>
    /// <param name="shuttleConsole"></param>
    /// <param name="ftlComp"></param>
    /// <param name="shuttleComp"></param>
    /// <param name="bypassDistanceCheck">Bypass for the distance check</param>
    /// <returns></returns>
    public bool FTLChecklist(TransformComponent transComp, EntityUid shuttleConsole, SignalSalvFtlDeviceComponent ftlComp, out ShuttleComponent? shuttleComp, bool bypassDistanceCheck = false)
    {
        shuttleComp = null;
        if (transComp.Anchored == false)
        {
            IngameConsoleHistoryChangeEvent ev = new("-> FTL CHANCELLED | FTL control is not anchored");
            RaiseLocalEvent(shuttleConsole, ref ev);
            return false;
        }
        if (transComp.GridUid == null)
        {
            IngameConsoleHistoryChangeEvent ev = new("-> FTL CHANCELLED | Lack of a grid");
            RaiseLocalEvent(shuttleConsole, ref ev);
            return false;
        }
        EntityUid gridUid = transComp.GridUid.Value;//we know you arent null
        TryComp<ShuttleComponent>(gridUid, out var shuttleComponent);
        if (shuttleComponent == null)
        {
            IngameConsoleHistoryChangeEvent ev = new("-> FTL CHANCELLED | The grid is not a SHUTTLE somehow");
            RaiseLocalEvent(shuttleConsole, ref ev);
            return false;
        }
        shuttleComp = shuttleComponent;
        //Check if the ship has FTL capabilities!!
        if (!CheckFTLAbility(gridUid, ftlComp, bypassDistanceCheck))
        {
            IngameConsoleHistoryChangeEvent ev = new("-> FTL CHANCELLED | The grid is not FTL capable (check <print FTL> for details)");
            RaiseLocalEvent(shuttleConsole, ref ev);
            return false;
        }
        if (ftlComp.CurrentlyLinkedMapUid == null)
        {
            return false;
        }
        return true;
    }
    private bool CheckFTLAbility(EntityUid gridUid, SignalSalvFtlDeviceComponent ftlComp, bool bypassDistanceCheck = false)
    {
        if (!TryComp<PhysicsComponent>(gridUid, out var physicsComp)) return false;
        TransformComponent transComp = Transform(gridUid);
        if (physicsComp!.Mass > ftlComp.MaxFTLGridMass) return false;
        if (ftlComp.FTLCharge > ftlComp.FTLCapacitiorsStoredCharge) return false;
        Vector2d vectorCord = new(transComp.Coordinates.X, transComp.Coordinates.Y);
        if (bypassDistanceCheck) return true;//if we dont care about distance skip the check
        if (ftlComp.JumpPointTolerance < GetDistance(vectorCord, ftlComp.DesignatedJumpPoint)) return false;
        return true;
    }
    private float GetDistance(Vector2d a, Vector2d b)
    {
        var c = Math.Pow(a.X + b.X, 2);
        var d = Math.Pow(a.Y + b.Y, 2);
        return (float) Math.Sqrt(c + d);
    }
    public void GenerateJumpPoint(SignalSalvFtlDeviceComponent ftlComp)
    {
        Random rand = new((int) _timing.CurTime.TotalSeconds);
        float angle = rand.NextFloat(0, (float) Math.PI * 2f);
        ftlComp.DesignatedJumpPoint = new(
            (float) Math.Cos(angle) * ftlComp.DistanceFromZeroZeroForJumpPoint,
            (float) Math.Sin(angle) * ftlComp.DistanceFromZeroZeroForJumpPoint
        );
        ftlComp.JumpPointSet = true;
        if (ftlComp.CurrentlyLinkedMapUid == null)
        {
            ftlComp.CurrentlyLinkedMapUid = GenerateExpeditionMap();
        }
        return;
    }
    /// <summary>
    /// Uses a linked disk that is inserted into the controll device to generate the Expedition Map
    /// </summary>
    /// <param name="ftlComp"></param>
    public void UsePredefinedSettingsJumpPointGeneration(EntityUid consoleUid, SignalSalvFtlDeviceComponent ftlComp)
    {
        if (!ftlComp.GenerationSettingsDiskSlot.HasItem)
        {
            IngameConsoleHistoryChangeEvent ev = new("-> Jump point could not be calculated due to a lack of a Destination Coordinate Disk (DCD)");
            RaiseLocalEvent(consoleUid, ref ev);//TODO: find the proper methode later
            return;
        }
        EntityUid expedMapUid = _mapSys.CreateMap(out var mapId);
        SignalSalvPlanetResourcesComponent planetResourcesComp = EnsureComp<SignalSalvPlanetResourcesComponent>(expedMapUid);
        EntityUid diskUid = (EntityUid) ftlComp.GenerationSettingsDiskSlot.Item!;//this SHOULD ALWAYS EXIST IF WE REACH THIS POINT
        if (!TryComp<SignalSalvGenerationSettingsComponent>(diskUid, out var settingsComp)) return;
        GenerateExpeditionMap(expedMapUid, mapId, planetResourcesComp, settingsComp);
        //we still need a random jump point and we already defined the LinkedMap so this just works
        GenerateJumpPoint(ftlComp);
    }
    public EntityUid GenerateExpeditionMap()
    {
        EntityUid expedMapUid = _mapSys.CreateMap(out var mapId);
        SignalSalvPlanetResourcesComponent planetResourcesComp = EnsureComp<SignalSalvPlanetResourcesComponent>(expedMapUid);
        GenerateExpeditionMap(expedMapUid, mapId, planetResourcesComp);
        return expedMapUid;
    }
    public void GenerateExpeditionMap(EntityUid expedMapUid, MapId mapId, SignalSalvPlanetResourcesComponent planetResourcesComp, SignalSalvGenerationSettingsComponent settingsComp)
    {
        Random rand = new((int) _timing.CurTime.TotalSeconds);
        CompleteMaterialList matMiningList = new();
        //first configure the planet type and mining rates
        planetResourcesComp.AdvancedResourcePlanet = settingsComp.AdvancedResourcePlanet;
        planetResourcesComp.SpecialResourcePlanet = settingsComp.SpecialResourcePlanet;
        if (settingsComp.PlanetResource.Count > 0)
        {
            planetResourcesComp.MiningRates.Union(settingsComp.PlanetResource);
        }
        GeneratePlanetMiningRates(planetResourcesComp);
        //Generate the POIs
        byte counter = rand.NextByte(planetResourcesComp.POIAmountMin, planetResourcesComp.POIAmountMax);
        counter -= (byte) settingsComp.POIApperanceList.Count;//this is never larger than 3 to 5 as of writing....[16.August.2026] TODO:reevaluate in future
        HashSet<double> takenAngles = new();
        foreach (var iterator in settingsComp.POIApperanceList)
        {
            TryLoadSingularPOI(planetResourcesComp, mapId, iterator, takenAngles, out takenAngles);
        }
        while (counter > 0)
        {
            counter--;
            TryGenerateSingularRandomPOI(planetResourcesComp, mapId, takenAngles, out takenAngles);
        }
        //now planetise and edit the planet
        _biome.EnsurePlanet(expedMapUid, _protoManager.Index(settingsComp.PlanetType), rand.Next());
        var moles = new float[Atmospherics.AdjustedNumberOfGases];//TODO actually allow configurable gas here!!!!
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;
        float temprature = settingsComp.AtmosTemp;
        var mixture = new GasMixture(moles, temprature);

        _atmos.SetMapAtmosphere(expedMapUid, false, mixture);
    }
    public void GenerateExpeditionMap(EntityUid expedMapUid, MapId mapId, SignalSalvPlanetResourcesComponent planetResourcesComp)
    {
        Random rand = new((int) _timing.CurTime.TotalSeconds);
        //now generate stuff:
        GeneratePlanetMiningRates(planetResourcesComp);
        //time to add the POIs
        byte counter = rand.NextByte(planetResourcesComp.POIAmountMin, planetResourcesComp.POIAmountMax);
        HashSet<double> takenAngles = new();
        while (counter > 0)
        {
            counter--;
            TryGenerateSingularRandomPOI(planetResourcesComp, mapId, takenAngles, out takenAngles);
        }
        //Turn map into a planet and add the outer barrier
        _biome.EnsurePlanet(expedMapUid, _protoManager.Index(BiomeTemplate), rand.Next());

    }
    private void GeneratePlanetMiningRates(SignalSalvPlanetResourcesComponent planetResourcesComp)
    {
        Random rand = new((int) _timing.CurTime.TotalSeconds);
        CompleteMaterialList matMiningList = new();
        bool selected = false;
        int failsaveCounter = 0;
        foreach (var iterator in matMiningList.BaseMaterials)//all base materials are always present
        {
            if (!iterator.Minable) continue;
            planetResourcesComp.MiningRates.Add(iterator.MaterialType, (int) rand.NextFloat(iterator.MinResoucePerSecond, iterator.MaxResoucePerSecond));
        }
        selected = false;
        failsaveCounter = 0;
        if (planetResourcesComp.AdvancedResourcePlanet)//generate a randomly selected one
        {
            while (!selected && failsaveCounter < 10)
            {
                failsaveCounter++;
                int randomIndex = (int) rand.NextInt64(matMiningList.AdvancedMaterials.Count);
                if (!matMiningList.AdvancedMaterials.ElementAt(randomIndex).Minable) continue;
                planetResourcesComp.MiningRates.Add(matMiningList.AdvancedMaterials.ElementAt(randomIndex).MaterialType,
                                            (int) rand.NextFloat(matMiningList.AdvancedMaterials.ElementAt(randomIndex).MinResoucePerSecond,
                                                                    matMiningList.AdvancedMaterials.ElementAt(randomIndex).MaxResoucePerSecond));
                selected = true;
            }

        }
        selected = false;
        failsaveCounter = 0;
        if (planetResourcesComp.SpecialResourcePlanet)//generate a randomly selected one
        {
            while (!selected && failsaveCounter < 10)
            {
                failsaveCounter++;
                int randomIndex = (int) rand.NextInt64(matMiningList.SpecialMaterials.Count);
                if (!matMiningList.SpecialMaterials.ElementAt(randomIndex).Minable) continue;
                planetResourcesComp.MiningRates.Add(matMiningList.SpecialMaterials.ElementAt(randomIndex).MaterialType,
                                            (int) rand.NextFloat(matMiningList.SpecialMaterials.ElementAt(randomIndex).MinResoucePerSecond,
                                                                    matMiningList.SpecialMaterials.ElementAt(randomIndex).MaxResoucePerSecond));
            }
        }
        return;
    }
    private bool TryGenerateSingularRandomPOI(SignalSalvPlanetResourcesComponent planetResourcesComp, MapId mapId, HashSet<double> takenAngles, out HashSet<double> takenAnglesOut)
    {
        takenAnglesOut = takenAngles;//set the default
        Random rand = new((int) _timing.CurTime.TotalSeconds);
        ResPath pOIlocation = new("/Maps/_Omu/Shuttles/CC-557.yml");//hardcoded default for now till proper selection is in!!!
        Vector2 offset = new();
        float distance = rand.NextFloat(planetResourcesComp.POIDistanceMin, planetResourcesComp.POIDistanceMax);
        int attempts = 0;
        bool invalidAngle = true;
        double angle = 0;
        while (invalidAngle && attempts < 15)
        {
            attempts++;
            invalidAngle = false;
            angle = rand.NextFloat(0.0f, (float) Math.PI * 2.0f);
            foreach (var iterator in takenAngles)
            {
                if (Math.Abs(angle - iterator) > planetResourcesComp.POIMinAngleDifference) invalidAngle = true;
            }
        }
        if (invalidAngle) return false;
        takenAngles.Add(angle);
        takenAnglesOut = takenAngles;//update it
        offset.X = (float) Math.Sin(angle) * distance;
        offset.Y = (float) Math.Cos(angle) * distance;
        return TryLoadSingularPOI(mapId, pOIlocation, offset);
    }
    private bool TryLoadSingularPOI(SignalSalvPlanetResourcesComponent planetResourcesComp, MapId mapId, ResPath pOIlocation, HashSet<double> takenAngles, out HashSet<double> takenAnglesOut)
    {
        takenAnglesOut = takenAngles;//set the default
        Random rand = new((int) _timing.CurTime.TotalSeconds);
        Vector2 offset = new();
        float distance = rand.NextFloat(planetResourcesComp.POIDistanceMin, planetResourcesComp.POIDistanceMax);
        int attempts = 0;
        bool invalidAngle = true;
        double angle = 0;
        while (invalidAngle && attempts < 15)
        {
            attempts++;
            invalidAngle = false;
            angle = rand.NextFloat(0.0f, (float) Math.PI * 2.0f);
            foreach (var iterator in takenAngles)
            {
                if (Math.Abs(angle - iterator) > planetResourcesComp.POIMinAngleDifference) invalidAngle = true;
            }
        }
        if (invalidAngle) return false;
        takenAngles.Add(angle);
        takenAnglesOut = takenAngles;//update it
        offset.X = (float) Math.Sin(angle) * distance;
        offset.Y = (float) Math.Cos(angle) * distance;
        return TryLoadSingularPOI(mapId, pOIlocation, offset);
    }
    private bool TryLoadSingularPOI(MapId mapId, ResPath pOIlocation, Vector2 offset)
    {
        return _mapLoader.TryLoadGrid(mapId, pOIlocation, out var gridOut, null, offset);
    }
    public void ReturnToStationSceduleDeleteMap(EntityUid shuttleConsole, SignalSalvFtlDeviceComponent ftlComp)
    {
        TransformComponent transComp = Transform(shuttleConsole);
        if (ftlComp.Originmap == null)
        {
            IngameConsoleHistoryChangeEvent ev = new("-> FTL CHANCELLED | No valid target to return");
            RaiseLocalEvent(shuttleConsole, ref ev);
            return;
        }
        if (!FTLChecklist(transComp, shuttleConsole, ftlComp, out var shuttleComponent, true)) return;
        ftlComp.DeleteLinkedMapOnFTLArrival = true;
        var targetCoordinates = new EntityCoordinates((EntityUid) ftlComp.Originmap!, ftlComp.DesignatedJumpPoint);
        Angle targetAngle = new();
        PostFTLCost(shuttleConsole, transComp.GridUid!.Value, ftlComp);
        _shuttle.FTLToCoordinates(transComp.GridUid!.Value, shuttleComponent!, targetCoordinates, targetAngle);
        return;
    }
    public void DeleteLinkedMap(Entity<SignalSalvFtlDeviceBasedFTLComponent> ent, ref FTLCompletedEvent args)
    {
        if (!TryComp<SignalSalvFtlDeviceComponent>(ent.Comp.LinkedFTLDevice, out var deviceComp)) return;
        RemCompDeferred<SignalSalvFtlDeviceBasedFTLComponent>(ent);
        if (deviceComp.DeleteLinkedMapOnFTLArrival == false) return;
        if (deviceComp.CurrentlyLinkedMapUid == null) return;
        foreach (var iterator in _mapSys.GetAllMapIds())//TODO: add a better methode to find the map ID, possibly safe it alond side/instead of the map UID
        {
            if (_mapSys.GetMap(iterator) == deviceComp.CurrentlyLinkedMapUid)
            {
                deviceComp.CurrentlyLinkedMapUid = null;
                _mapSys.QueueDeleteMap(iterator);
                deviceComp.DeleteLinkedMapOnFTLArrival = false;
                break;
            }
        }
        return;
    }
    #endregion
}
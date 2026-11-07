using System.Numerics;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Map;

namespace Content.Omu.Server._BSD.SignalSalv.Components;

//This exists to allow me to modify specail values later such as true distance of objects in LY and is also used as a console UI detection.

[RegisterComponent]
public sealed partial class SignalSalvFtlDeviceComponent : Component
{
    [DataField]
    public float MaxFTLGridMass = 100.0f;

    /// <summary>
    /// Amount of J needed to perform a FTL jump, yes this is a lot I recomend getting power from engi
    /// </summary>
    [DataField]
    public Int64 FTLCharge = 1000000;

    [DataField]
    public Int64 FTLCapacitiorsStoredCharge = 0;

    /// <summary>
    /// Amount of J moved to the FTL capacitors every second
    /// </summary>
    [DataField]
    public Int64 FTLCapacitiorChargeRate = 25000;

    /// <summary>
    /// percentrage efficency
    /// </summary>
    [DataField]
    public int FTLCapacitiorChargeEfficency = 50;

    [DataField]
    public float DistanceFromZeroZeroForJumpPoint = 300.0f;

    [DataField]
    public float JumpPointTolerance = 25.0f;

    /// <summary>
    /// Location where the FTL jump will be taking place
    /// </summary>
    [DataField]
    public Vector2 DesignatedJumpPoint = new();

    /// <summary>
    /// Location where the FTL jump will be taking place
    /// </summary>
    [DataField]
    public bool JumpPointSet = false;

    /// <summary>
    /// Location where the FTL jump will be taking place
    /// </summary>
    [DataField]
    public bool PreConfigedPlanet = false;

    [DataField]
    public TimeSpan LastUpdate;

    /// <summary>
    /// save the map we are comming from (in case we somehow have multiple stations or station maps)
    /// </summary>
    [DataField]
    public EntityUid? Originmap;


    /// <summary>
    /// saved map we generated so we know what to delete when we FTL away and WHERE to FTL too
    /// </summary>
    [DataField]
    public EntityUid? CurrentlyLinkedMapUid;


    /// <summary>
    /// Stores the Datadisk for the planet generation
    /// </summary>
    [DataField]
    public ItemSlot GenerationSettingsDiskSlot = new();

    [DataField]
    public bool DeleteLinkedMapOnFTLArrival = false;
}

/// <summary>
/// Special FTL component that overrides the FTL cooldowns cause ye know a special machine did it also needed to delete the MAPs after
/// </summary>
[RegisterComponent]
public sealed partial class SignalSalvFtlDeviceBasedFTLComponent : Component
{
    [DataField]
    public EntityUid LinkedFTLDevice;
}

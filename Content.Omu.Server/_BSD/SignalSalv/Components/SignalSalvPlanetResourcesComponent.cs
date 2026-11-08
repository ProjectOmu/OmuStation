using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Content.Shared.Random;

namespace Content.Omu.Server._BSD.SignalSalv.Components;

[RegisterComponent]
public sealed partial class SignalSalvPlanetResourcesComponent : Component
{
    /// <summary>
    /// Mining rate
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<MaterialPrototype>, int> MiningRates { get; set; } = new();

    /// <summary>
    /// if the planet contains advanced resources -> gold, silver, uranium
    /// </summary>
    [DataField]
    public bool AdvancedResourcePlanet = false;

    /// <summary>
    /// if the planet contains special resources -> plasma
    /// </summary>
    [DataField]
    public bool SpecialResourcePlanet = false;

    /// <summary>
    /// min number of POI
    /// </summary>
    [DataField]
    public byte POIAmountMin = 1;

    /// <summary>
    /// max numbder of POI
    /// </summary>
    [DataField]
    public byte POIAmountMax = 3;

    /// <summary>
    /// mind distance for a POI
    /// </summary>
    [DataField]
    public float POIDistanceMin = 100;

    /// <summary>
    /// max distance for POI
    /// </summary>
    [DataField]
    public float POIDistanceMax = 250;

    /// <summary>
    /// in PI radiay
    /// </summary>
    [DataField]
    public float POIMinAngleDifference = 0.2f;

    /// <summary>
    /// Determiens the effects this disease mutates
    /// </summary>
    [DataField]
    public ProtoId<WeightedRandomPrototype> AvailablePois = "SignalSalvPoi";
}

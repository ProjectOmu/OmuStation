using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Atmos;
using Robust.Shared.Utility;

namespace Content.Omu.Server._BSD.SignalSalv.Components;

[RegisterComponent]
public sealed partial class SignalSalvGenerationSettingsComponent : Component
{
    /// <summary>
    /// Contains the POIs that will be on the exped
    /// Figure out how the fuck we can get the POI map prototypes
    /// </summary>
    [DataField]
    public HashSet<ResPath> POIApperanceList = new();

    /// <summary>
    /// Defaults to Continental
    /// declares what planet biom is used
    /// </summary>
    [DataField]
    public ProtoId<BiomeTemplatePrototype> PlanetType = "Continental";

    /// <summary>
    /// Atmospheric temprature in K
    /// </summary>
    [DataField]
    public float AtmosTemp = Atmospherics.T20C;

    /// <summary>
    /// Atmospheric composition in mols
    /// </summary>
    [DataField]
    public int[] AtmosComposition;

    /// <summary>
    /// material name -> Amount per second / 100 || aka 1 is 0.01/s and 100 is 1/s
    /// Only used if Signal SCI involved
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<MaterialPrototype>, int> PlanetResource;

    /// <summary>
    /// Prevents advanced materials from generating -> Silver,Gold,Uranium
    /// </summary>
    [DataField]
    public bool AdvancedResourcePlanet = false;

    /// <summary>
    /// Prevents special materials from generating -> Plasma
    /// </summary>
    [DataField]
    public bool SpecialResourcePlanet = false;
}

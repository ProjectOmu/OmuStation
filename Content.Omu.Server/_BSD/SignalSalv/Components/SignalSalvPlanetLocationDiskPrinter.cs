using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Atmos;
using Robust.Shared.Utility;

namespace Content.Omu.Server._BSD.SignalSalv.Components;

[RegisterComponent]
public sealed partial class SignalSalvPlanetLocationDiskPrinterComponent : Component
{
    [DataField]
    public int PrintingCost = 0;
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> PlanetResource;
    [DataField]
    public float PlanetResourceMaterialPriceModifier = 1.0f;

    [DataField]
    public bool AdvancedResourcePlanet = false;
    [DataField]
    public int AdvancedResourcePlanetCost = 50000;

    [DataField]
    public bool SpecialResourcePlanet = false;
    [DataField]
    public int SpecialResourcePlanetCost = 250000;
    [DataField]
    public bool InvalidConfiguration = false;
    [DataField]
    public EntProtoId DiskPrototype = "SignalSalvPlanetSettingDisk";
}

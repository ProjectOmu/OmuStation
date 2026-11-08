using Content.Omu.Server._BSD.SignalSalv.Components;
using Content.Omu.Server._BSD.SignalSalv.Helpers;

namespace Content.Omu.Server._BSD.SignalSalv;

public sealed partial class BSDSignalSalvSystem : EntitySystem
{
    public void GeneratePlanetDisk()
    {
        return;
    }
    public void CalculateCost(SignalSalvPlanetLocationDiskPrinterComponent comp)
    {
        comp.InvalidConfiguration = false;
        comp.PrintingCost = 0;
        if (comp.AdvancedResourcePlanet) comp.PrintingCost += comp.AdvancedResourcePlanetCost;
        if (comp.SpecialResourcePlanet) comp.PrintingCost += comp.SpecialResourcePlanetCost;
        if (comp.AdvancedResourcePlanet && comp.SpecialResourcePlanet) comp.InvalidConfiguration = true;
        foreach (var iterator in comp.PlanetResource.Keys)
        {
            if (MaterialMiningRatesBase.AllMaterialsDict[iterator].SignificantDataCostPerMiningUnit == int.MaxValue)
            {
                comp.InvalidConfiguration = true;
                continue;
            }
            comp.PrintingCost += (int) MathF.Round(MaterialMiningRatesBase.AllMaterialsDict[iterator].SignificantDataCostPerMiningUnit * comp.PlanetResource[iterator] * comp.PlanetResourceMaterialPriceModifier);
        }
    }
}
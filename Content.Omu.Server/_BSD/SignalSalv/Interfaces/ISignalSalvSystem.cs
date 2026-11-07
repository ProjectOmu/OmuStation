
using JetBrains.Annotations;
using Content.Omu.Server._BSD.SignalSalv.Components;

namespace Content.Omu.Server._BSD.SignalSalv
{
    [PublicAPI]
    public partial interface IBSDSignalSalvMaterialTransitSystem
    {
        bool ValidMaterial(String materialType);
        void OverrideProductionRate(string type, int newAmount);
    }
}
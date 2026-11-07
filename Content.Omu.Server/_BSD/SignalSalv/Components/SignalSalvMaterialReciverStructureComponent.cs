using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.SignalSalv.Components;

[RegisterComponent]
public sealed partial class SignalSalvMaterialReciverStructureComponent : Component
{
    /// <summary>
    /// Min amount of material before a delivery is made -> aka if it is at 1000 it means 10 steel need to be mined and will then be deposited
    /// Can be upgraded to need less and less till it reaches 1 and is instant via bluespace tech
    /// </summary>
    [DataField]
    public int MaterialCargoMin = 1000;

    /// <summary>
    /// Multistruct types that lower MaterialCargoMin
    /// </summary>
    [DataField]
    public string[] MaterialDeliverySpeedBoosterTypes = { "MaterialDeliveryIntervalBooster" };

    /// <summary>
    /// if one area needs to be exposed to space
    /// </summary>
    [DataField]
    public bool RequireSpaceFacingRecivers = true;
}

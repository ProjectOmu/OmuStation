using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
namespace Content.Server._DV.CartridgeLoader.Cartridges;

[RegisterComponent, Access(typeof(StockTradingCartridgeSystem))]
public sealed partial class StockTradingCartridgeComponent : Component
{
    /// <summary>
    /// Station entity to keep track of
    /// </summary>
    [DataField]
    public EntityUid? Station;

    /// <summary>
    /// The account that this console pulls from for ordering.
    /// </summary>
    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Cargo";        // Omu fix since we use seperate bank accounts for each dept
}

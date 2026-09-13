using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cryogenics;

public sealed partial class InsideCryoPodComponent: Component
{
    /// <summary>
    /// Store the original temperature transfer for species if any
    /// </summary>
    [DataField]
    public float? OriginalAtmosTemperatureTransferEfficiency;
}

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content._Omu.Shared.EntityEffects.Effects;

public sealed partial class ReduceFascinationEntityEffect : EventEntityEffect<ReduceFascinationEntityEffect>
{
    /// <summary>
    /// how much fascination to remove per cycle
    /// </summary>
    [DataField]
    public float ToChange = -0.2f;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-reduce-fascination", ("chance", Probability));
    }
}

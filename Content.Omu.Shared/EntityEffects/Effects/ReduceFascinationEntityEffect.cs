using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ReduceFascinationEntityEffect : EntityEffectBase<ReduceFascinationEntityEffect>
{
    /// <summary>
    /// how much fascination to remove per cycle
    /// </summary>
    [DataField]
    public float ToChange = -0.2f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-reduce-fascination", ("chance", Probability));
}

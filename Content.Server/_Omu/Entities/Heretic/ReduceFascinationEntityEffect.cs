using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Entities.Heretic;

[UsedImplicitly]
public sealed partial class ReduceFascinationEntityEffect : EntityEffect
{
    /// <summary>
    /// how much fascination to remove per cycle
    /// </summary>
    [DataField]
    public float ToChange = 0.2f;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-drop-items", ("chance", Probability));
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var ev = new FascinationChangedArgs();
        ev.Amount = ToChange;
        args.EntityManager.EventBus.RaiseLocalEvent(args.TargetEntity, ev);
    }
}

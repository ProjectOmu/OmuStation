using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._FarHorizons.Vampire;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.EntityEffects.Effects.Vampire;

public sealed partial class FillBloodPool : EntityEffect
{
    [DataField]
    public FixedPoint2 Factor = 1f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entityManager = args.EntityManager;
        if (!entityManager.TryGetComponent<LesserVampireComponent>(args.TargetEntity, out var vampireComponent))
            return;

        var vampireSystem = entityManager.System<SharedLesserVampireSystem>();

        FixedPoint2 mult = 0f;
        if (args is EntityEffectReagentArgs reagentArgs)
            mult = reagentArgs.Quantity * reagentArgs.Scale;

        Entity<LesserVampireComponent> ent = (args.TargetEntity, vampireComponent);
        var current = vampireSystem.GetBloodPool(ent);

        var amt = Factor * mult;
        var newPool = current + amt;
        vampireSystem.SetBloodPool(ent, (float) newPool);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-fill-blood-pool", ("relative", (float) Factor));
}

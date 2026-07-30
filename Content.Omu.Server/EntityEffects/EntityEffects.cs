using Content.Shared.EntityEffects;
using Content.Omu.Shared.EntityEffects.Effects;
using Content.Omu.Shared.Entities.Heretic;

namespace Content.Server.EntityEffects;

public sealed class EntityEffectSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecuteEntityEffectEvent<ReduceFascinationEntityEffect>>(OnReduceFascination); // Omu
    }
    private void OnReduceFascination(ref ExecuteEntityEffectEvent<ReduceFascinationEntityEffect> args)
    {
        EnsureComp<FascinationComponent>(args.Args.TargetEntity);
        RaiseLocalEvent(args.Args.TargetEntity, new FascinationChangedArgs { Amount = args.Effect.ToChange });
    }
}

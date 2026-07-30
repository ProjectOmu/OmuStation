using Content.Shared.EntityEffects;
using Content._Omu.Server.Entities.Heretic; // Omu
using Content._Omu.Shared.EntityEffects.Effects; // Omu

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

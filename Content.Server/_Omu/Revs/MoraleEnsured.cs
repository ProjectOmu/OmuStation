using Robust.Shared.Timing;

namespace Content.Server._Omu.Revs;

[RegisterComponent, Access(typeof(MoralePassedSystem), typeof(MoraleSystem))]
public sealed partial class MoralePassedComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float Time = 60f; //One would have thought a timespan would have been better. One was wrong.

    [ViewVariables(VVAccess.ReadOnly)]
    public float UpdateAccumulator = 0f;
}

public sealed class MoralePassedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var query = EntityManager.EntityQuery<MoralePassedComponent>();

        foreach (var comp in query)
        {
            if (TerminatingOrDeleted(comp.Owner))
                continue;

            comp.UpdateAccumulator += frameTime;

            if (comp.UpdateAccumulator >= comp.Time)
            {
                RemCompDeferred<MoralePassedComponent>(comp.Owner);
            }
        }
    }
}

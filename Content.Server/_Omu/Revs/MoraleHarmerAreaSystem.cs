using Content.Shared.Humanoid;
using Content.Shared.Revolutionary.Components;
using Content.Server.Mind;
using Robust.Shared.Timing;
namespace Content.Server._Omu.Revs;

public sealed class MoraleHarmerAreaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    public override void Initialize()
    {
        base.Initialize();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var query = EntityManager.EntityQuery<MoraleHarmerAreaComponent>();

        foreach (var moraleHarmer in query)
        {
            if (TerminatingOrDeleted(moraleHarmer.Owner))
                continue;

            moraleHarmer.UpdateAccumulator += frameTime;

            if (moraleHarmer.UpdateAccumulator >= moraleHarmer.UpdateTimer)
            {
                moraleHarmer.UpdateAccumulator -= moraleHarmer.UpdateTimer;
                AreaChange(new Entity<MoraleHarmerAreaComponent>(moraleHarmer.Owner, moraleHarmer));
            }
        }
    }

    public void AreaChange(Entity<MoraleHarmerAreaComponent> ent)
    {
        var xform = Transform(ent);
        var lookup = _lookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range);
        foreach (var target in lookup)
        {
            if (!_mind.TryGetMind(ent, out _, out _))
                continue;

            if (!HasComp<HumanoidAppearanceComponent>(target))
                continue;   // Break loop since its an object

            if (HasComp<RevolutionaryComponent>(target))
                continue; // Already revved

            if (TryComp<MoraleComponent>(target, out var morale))
            {
                var ev = new MoraleChangedArgs
                {
                    Amount = ent.Comp.MoraleChange,

                    User = ent,
                };
                RaiseLocalEvent(target, ev);
                continue; //Break loop since we have reduced morale
            }

            EnsureComp<MoraleComponent>(target);       //Ensure morale comp.
        }
    }

    public void AreaChange(EntityUid ent, float amount, float range)
    {
        var xform = Transform(ent);
        var lookup = _lookup.GetEntitiesInRange(xform.Coordinates, range);
        foreach (var target in lookup)
        {
            if (!_mind.TryGetMind(ent, out _, out _))
                continue;

            if (!HasComp<HumanoidAppearanceComponent>(target))
                continue;   // Break loop since its an object

            if (HasComp<RevolutionaryComponent>(target))
                continue; // Already revved

            if (TryComp<MoraleComponent>(target, out var morale))
            {
                var ev = new MoraleChangedArgs
                {
                    Amount = amount,

                    User = ent,
                };
                RaiseLocalEvent(target, ev);
                continue; //Break loop since we have reduced morale
            }

            EnsureComp<MoraleComponent>(target);       //Ensure morale comp.
        }
    }
}

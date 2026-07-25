using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FarHorizons.Vampire.Traits.Negative;

public sealed class PassiveBloodPoolDrainVampireTraitSystem : LesserVampireTraitSystem<PassiveBloodPoolDrainVampireTraitComponent>;

public sealed class BloodDependencyVampireTraitSystem : LesserVampireTraitSystem<BloodDependencyVampireTraitComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodDependencyVampireTraitComponent, OutOfBloodPoolEvent>(OnOutOfBloodPool);
        SubscribeLocalEvent<BloodDependencyVampireTraitComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<BloodDependencyVampireTraitComponent> ent, ref MobStateChangedEvent args) =>
        ent.Comp.ImmuneUntil = _timing.CurTime + ent.Comp.ImmunityAfterStateChange;

    private void OnOutOfBloodPool(Entity<BloodDependencyVampireTraitComponent> ent, ref OutOfBloodPoolEvent args)
    {
        if (_timing.CurTime < ent.Comp.ImmuneUntil) return;

        _damage.TryChangeDamage(ent.Owner, ent.Comp.Damage, true, false);
    }
}

public sealed class DefangedVampireTraitSystem : LesserVampireTraitSystem<DefangedVampireTraitComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DefangedVampireTraitComponent, VampireFangsCheck>(OnFangsCheck);
        SubscribeLocalEvent<DefangedVampireTraitComponent, VampireBiteCheck>(OnBiteCheck);
    }

    private void OnBiteCheck(Entity<DefangedVampireTraitComponent> ent, ref VampireBiteCheck args) =>
        args.Cancelled = true;

    private void OnFangsCheck(Entity<DefangedVampireTraitComponent> ent, ref VampireFangsCheck args) =>
        args.FangsHidden = true;
}

public sealed class GourmandVampireTraitSystem : LesserVampireTraitSystem<GourmandVampireTraitComponent>
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GourmandVampireTraitComponent, VampireBiteCheck>(OnBiteCheck);
    }

    private void OnBiteCheck(Entity<GourmandVampireTraitComponent> ent, ref VampireBiteCheck args)
    {
        switch (ent.Comp.Inverted)
        {
            case false when
                !_mobState.IsAlive(args.Target) ||
                _mind.GetMind(args.Target) == null:
            case true when
                _mobState.IsAlive(args.Target) &&
                _mind.GetMind(args.Target) != null:
                args.Cancelled = true;
                break;
        }
    }
}
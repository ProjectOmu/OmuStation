using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._Omu.Traits;

public sealed class NoCritSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NoCritComponent, ComponentStartup>(OnStartup);
    }

    // Core of the NoCrit system, forces the entity to skip the Crit state
    private void OnStartup(EntityUid uid, NoCritComponent component, ComponentStartup args)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        var maxHp = 100;

        _thresholds.SetMobStateThreshold(uid, maxHp, MobState.Critical, thresholds);
        _thresholds.SetMobStateThreshold(uid, maxHp, MobState.Dead, thresholds);
    }
}

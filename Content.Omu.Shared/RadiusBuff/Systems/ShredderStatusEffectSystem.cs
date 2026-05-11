using Content.Omu.Shared.RadiusBuff.Components;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Omu.Shared.RadiusBuff.Systems;

public sealed class ShredderStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly DamageableSystem _dmg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<ShredderStatusEffectComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextCheck >= _timing.CurTime)
                continue;

            if (!TryComp<StatusEffectComponent>(uid, out var statusComp) || statusComp.AppliedTo == null)
                continue;

            var target = statusComp.AppliedTo.Value;

            TryApplyHealing(target, comp);
            _movement.RefreshMovementSpeedModifiers(target);

            if (!_net.IsClient && comp.Visual != null)
            {
                var effect = Spawn(comp.Visual, Transform(target).Coordinates);
                _transform.SetParent(effect, Transform(effect), target);
            }

            comp.NextCheck = _timing.CurTime + comp.Delay;

            Dirty(uid, comp);
        }
    }

    private bool TryApplyHealing(EntityUid target, ShredderStatusEffectComponent comp)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return false;

        _dmg.TryChangeDamage(target,
                comp.HealBuff,
                true,
                false,
                damageable,
                targetPart: TargetBodyPart.All,
                splitDamage: SplitDamageBehavior.SplitEnsureAll);

        return true;
    }
}

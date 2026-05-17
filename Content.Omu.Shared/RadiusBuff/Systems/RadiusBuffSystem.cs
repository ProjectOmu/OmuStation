using Content.Omu.Shared.RadiusBuff.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Whitelist;
using Robust.Shared.Timing;

namespace Content.Omu.Shared.RadiusBuff.Systems;

public sealed class RadiusBuffSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<RadiusBuffComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Active)
                continue;

            if (comp.NextCheck >= _timing.CurTime)
                continue;

            var lookup = _lookup.GetEntitiesInRange(xform.Coordinates, comp.Range);
            foreach (var target in lookup)
            {
                if (Allowed(uid, target, comp))
                    _status.TrySetStatusEffectDuration(target, comp.Status, comp.Duration);
            }

            comp.NextCheck = _timing.CurTime + comp.Delay;

            Dirty(uid, comp);
        }
    }

    private bool Allowed(EntityUid uid, EntityUid target, RadiusBuffComponent comp)
    {
        if (_whitelist.IsWhitelistPass(comp.Whitelist, target) && !_whitelist.IsWhitelistPass(comp.Blacklist, target))
            return true;

        if (comp.BuffSelf && uid == target)
            return true;

        if (comp.BuffHolder && _hands.IsHolding(target, uid))
            return true;

        return false;
    }
}

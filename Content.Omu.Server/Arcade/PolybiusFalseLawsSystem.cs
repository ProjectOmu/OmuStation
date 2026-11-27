// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Arcade;
using Content.Server._Impstation.Thaven;
using Content.Server.Silicons.Laws;
using Content.Shared._Impstation.Thaven.Components;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Arcade;

public sealed class PolybiusFalseLawsSystem : EntitySystem
{
    [Dependency] private readonly PolybiusAttackEffectsSystem _attackEffects = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SiliconLawSystem _siliconLaw = default!;
    [Dependency] private readonly ThavenMoodsSystem _thavenMoods = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PolybiusFalseLawsComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PolybiusFalseLawsComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextAlertTime = _timing.CurTime
            + _random.Next(ent.Comp.MinTimeBetweenAlerts, ent.Comp.MaxTimeBetweenAlerts);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<PolybiusFalseLawsComponent>();
        while (query.MoveNext(out var uid, out var falseLaws))
        {
            if (falseLaws.NextAlertTime > _timing.CurTime)
                continue;

            falseLaws.NextAlertTime = _timing.CurTime
                + _random.Next(falseLaws.MinTimeBetweenAlerts, falseLaws.MaxTimeBetweenAlerts);

            if (_attackEffects.IsShowingAttackLines(uid) || !TryAnnounceChange(uid))
                continue;

            falseLaws.Alerts++;
            Dirty(uid, falseLaws);
        }
    }

    private bool TryAnnounceChange(EntityUid uid)
    {
        if (TryComp<ThavenMoodsComponent>(uid, out var moods))
        {
            _thavenMoods.NotifyMoodChange((uid, moods));
            return true;
        }

        if (!HasComp<SiliconLawBoundComponent>(uid))
            return false;

        _siliconLaw.NotifyLawsChanged(uid, CompOrNull<SiliconLawProviderComponent>(uid)?.LawUploadSound);
        return true;
    }
}

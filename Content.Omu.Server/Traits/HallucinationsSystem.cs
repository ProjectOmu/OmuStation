// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Traits;

public sealed class HallucinationsSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HallucinationsComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HallucinationsComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextIncidentTime = _timing.CurTime
            + _random.Next(ent.Comp.MinTimeBetweenIncidents, ent.Comp.MaxTimeBetweenIncidents);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<HallucinationsComponent>();
        while (query.MoveNext(out var uid, out var hallucinations))
        {
            if (hallucinations.NextIncidentTime > _timing.CurTime)
                continue;

            var duration = _random.Next(hallucinations.MinDurationOfIncident, hallucinations.MaxDurationOfIncident);
            hallucinations.NextIncidentTime += duration
                + _random.Next(hallucinations.MinTimeBetweenIncidents, hallucinations.MaxTimeBetweenIncidents);

            _statusEffects.TryAddStatusEffectDuration(uid, hallucinations.StatusEffect, duration);
        }
    }
}

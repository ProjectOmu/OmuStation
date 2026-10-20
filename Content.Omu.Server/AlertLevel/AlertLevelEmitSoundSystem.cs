// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared.Sound.Components;

namespace Content.Omu.Server.AlertLevel;

public sealed class AlertLevelEmitSoundSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<AlertLevelEmitSoundComponent, MapInitEvent>(OnMapInit);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        using var query = EntityQueryEnumerator<AlertLevelEmitSoundComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            SetSound((uid, comp), args.AlertLevel);
        }
    }

    private void OnMapInit(Entity<AlertLevelEmitSoundComponent> ent, ref MapInitEvent args)
    {
        var station = _station.GetOwningStation(ent.Owner);
        SetSound(ent, CompOrNull<AlertLevelComponent>(station)?.CurrentLevel);
    }

    private void SetSound(Entity<AlertLevelEmitSoundComponent> ent, string? level)
    {
        var sound = level != null && ent.Comp.Sounds.TryGetValue(level, out var levelSound)
            ? levelSound
            : ent.Comp.DefaultSound;

        if (TryComp<EmitSoundOnUseComponent>(ent, out var use))
        {
            use.Sound = sound;
            Dirty(ent, use);
        }

        if (TryComp<EmitSoundOnActivateComponent>(ent, out var activate))
        {
            activate.Sound = sound;
            Dirty(ent, activate);
        }
    }
}

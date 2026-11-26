// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Shared.Sandevistan;
using Content.Omu.Server.StationAI;
using Content.Omu.Shared.Arcade;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared._Imp.Drone;
using Content.Shared.Bed.Sleep;
using Content.Shared.Dataset;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Arcade;

public sealed class PolybiusAttackEffectsSystem : EntitySystem
{
    private static readonly ProtoId<SpeciesPrototype> Diona = "Diona";

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationAiEyePopupSystem _eyePopup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PolybiusAttackEffectsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PolybiusAttackEffectsComponent, TryingToSleepEvent>(OnTryingToSleep);
    }

    private void OnMapInit(Entity<PolybiusAttackEffectsComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUnlockTime = _timing.CurTime
            + _random.Next(ent.Comp.MinTimeBetweenUnlocks, ent.Comp.MaxTimeBetweenUnlocks);
        ent.Comp.NextFreezeTime = _timing.CurTime
            + _random.Next(ent.Comp.MinTimeBetweenFreezes, ent.Comp.MaxTimeBetweenFreezes);
    }

    private bool IsFreezingMachine(EntityUid uid)
    {
        return HasComp<BorgChassisComponent>(uid)
            || HasComp<DroneComponent>(uid)
            || HasComp<StationAiHeldComponent>(uid);
    }

    public bool UsesFreezeAttacks(EntityUid uid)
    {
        return IsFreezingMachine(uid) || CompOrNull<HumanoidAppearanceComponent>(uid)?.Species == Diona;
    }

    public bool IsShowingAttackLines(EntityUid uid)
    {
        return TryComp<PolybiusAttackEffectsComponent>(uid, out var effects) && effects.PendingLines.Count > 0;
    }

    private void OnTryingToSleep(Entity<PolybiusAttackEffectsComponent> ent, ref TryingToSleepEvent args)
    {
        if (UsesFreezeAttacks(ent)
            || !_statusEffects.TryGetTime(ent, SleepingSystem.StatusEffectForcedSleeping, out var time)
            || time.EndEffectTime is not { } endTime)
            return;

        var duration = endTime - _timing.CurTime;

        if (!HasComp<SiliconComponent>(ent))
        {
            StartParanoia(ent, ent.Comp, duration);
            return;
        }

        _statusEffects.TrySetStatusEffectDuration(ent, ent.Comp.BlackoutEffect, duration);
        StartSiliconAttack(ent, ent.Comp, duration);
    }

    private void StartSiliconAttack(EntityUid uid, PolybiusAttackEffectsComponent effects, TimeSpan duration)
    {
        var lines = PickLines(effects.Diagnostics, effects.MiddleLines);
        QueueLines(effects, effects.DiagnosticStart, lines, effects.DiagnosticEnd, duration);
        effects.LinesArePublic = true;

        var glitch = EnsureComp<SandevistanGlitchComponent>(uid);
        glitch.ExpiresAt = _timing.CurTime + duration;
        Dirty(uid, glitch);

        if (HasComp<StationAiHeldComponent>(uid))
            return;

        _audio.PlayPvs(effects.DiagnosticSound, uid);

        if (!TryComp<PolybiusLightFlickerComponent>(uid, out var flicker))
            return;

        flicker.EndTime = _timing.CurTime + flicker.Duration;
        Dirty(uid, flicker);
    }

    private void TryFreeze(EntityUid uid, PolybiusAttackEffectsComponent effects)
    {
        if (effects.NextFreezeTime > _timing.CurTime)
            return;

        var duration = _random.Next(effects.MinFreezeDuration, effects.MaxFreezeDuration);
        effects.NextFreezeTime = _timing.CurTime + duration
            + _random.Next(effects.MinTimeBetweenFreezes, effects.MaxTimeBetweenFreezes);

        _stun.TryUpdateStunDuration(uid, duration);

        if (IsFreezingMachine(uid))
            StartSiliconAttack(uid, effects, duration);
        else
            StartParanoia(uid, effects, duration);
    }

    private void StartParanoia(EntityUid uid, PolybiusAttackEffectsComponent effects, TimeSpan duration)
    {
        var lines = new List<string>();

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
            && effects.SpeciesParanoia.TryGetValue(humanoid.Species, out var speciesLines))
            lines = PickLines(speciesLines, effects.SpeciesLines);

        lines.AddRange(PickLines(effects.Paranoia, effects.MiddleLines - lines.Count));
        _random.Shuffle(lines);

        QueueLines(effects, effects.ParanoiaStart, lines, effects.ParanoiaEnd, duration);
        effects.LinesArePublic = false;
    }

    private void HoldAiEyeStill(EntityUid uid)
    {
        if (HasComp<StunnedComponent>(uid) && _eyePopup.GetEye(uid) is { } eye)
            _physics.SetLinearVelocity(eye, Vector2.Zero);
    }

    private List<string> PickLines(ProtoId<LocalizedDatasetPrototype> dataset, int count)
    {
        var lines = _prototype.Index(dataset).Values.ToList();
        _random.Shuffle(lines);
        return lines.Take(count).ToList();
    }

    private void QueueLines(PolybiusAttackEffectsComponent effects,
        LocId start,
        List<string> middle,
        LocId end,
        TimeSpan duration)
    {
        effects.PendingLines.Clear();
        effects.PendingLines.Enqueue(start);

        foreach (var line in middle)
        {
            effects.PendingLines.Enqueue(line);
        }

        effects.PendingLines.Enqueue(end);
        effects.LineInterval = duration / effects.PendingLines.Count;
        effects.NextLineTime = _timing.CurTime;
    }

    private void ShowNextLine(EntityUid uid, PolybiusAttackEffectsComponent effects)
    {
        if (effects.NextLineTime > _timing.CurTime || !effects.PendingLines.TryDequeue(out var line))
            return;

        effects.NextLineTime += effects.LineInterval;

        if (effects.LinesArePublic && !HasComp<StationAiHeldComponent>(uid))
            _popup.PopupEntity(Loc.GetString(line), uid, PopupType.Medium);
        else
            _eyePopup.PopupToSelf(Loc.GetString(line), uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<PolybiusAttackEffectsComponent>();
        while (query.MoveNext(out var uid, out var effects))
        {
            ShowNextLine(uid, effects);

            var freezes = UsesFreezeAttacks(uid);
            if (freezes)
            {
                TryFreeze(uid, effects);
                HoldAiEyeStill(uid);
            }

            if (effects.NextUnlockTime > _timing.CurTime
                || (!IsFreezingMachine(uid) && !HasComp<SiliconComponent>(uid)))
                continue;

            effects.NextUnlockTime = _timing.CurTime
                + _random.Next(effects.MinTimeBetweenUnlocks, effects.MaxTimeBetweenUnlocks);

            if (effects.PendingLines.Count > 0)
                continue;

            var isAi = HasComp<StationAiHeldComponent>(uid);
            _eyePopup.PopupToSelf(Loc.GetString(isAi ? effects.StationAiIntruderPopup : effects.UnlockPopup), uid);
            _audio.PlayGlobal(isAi ? effects.StationAiIntruderSound : effects.UnlockSound, uid);
        }
    }
}

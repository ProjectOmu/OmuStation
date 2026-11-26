// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Arcade;

public sealed class PolybiusSpeciesScaresSystem : EntitySystem
{
    [Dependency] private readonly PolybiusAttackEffectsSystem _attackEffects = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PolybiusSpeciesScaresComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PolybiusSpeciesScaresComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextScareTime = _timing.CurTime
            + _random.Next(ent.Comp.MinTimeBetweenScares, ent.Comp.MaxTimeBetweenScares);
    }

    private void StartScare(EntityUid uid, PolybiusSpeciesScaresComponent scares, PolybiusSpeciesScare scare)
    {
        if (scare.Emote is { } emote && !_chat.TryEmoteWithChat(uid, emote))
            return;

        scares.PendingScare = scare;
        scares.PendingScareTime = _timing.CurTime + scare.Delay;
    }

    private void FinishScare(EntityUid uid, PolybiusSpeciesScare scare)
    {
        var coordinates = Transform(uid).Coordinates;

        _popup.PopupEntity(Loc.GetString(scare.Popup), uid, uid, scare.PopupType);
        _audio.PlayStatic(scare.Sound, uid, coordinates.Offset(_random.NextVector2(scare.SoundDistance)));

        if (scare.Spawn is { } spawn)
            Spawn(spawn, coordinates);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<PolybiusSpeciesScaresComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var scares, out var humanoid))
        {
            if (scares.PendingScare is { } pending && scares.PendingScareTime <= _timing.CurTime)
            {
                scares.PendingScare = null;
                FinishScare(uid, pending);
            }

            if (scares.NextScareTime > _timing.CurTime)
                continue;

            scares.NextScareTime = _timing.CurTime
                + _random.Next(scares.MinTimeBetweenScares, scares.MaxTimeBetweenScares);

            if (scares.PendingScare != null
                || _attackEffects.IsShowingAttackLines(uid)
                || !scares.Scares.TryGetValue(humanoid.Species, out var options))
                continue;

            StartScare(uid, scares, _random.Pick(options));
        }
    }
}

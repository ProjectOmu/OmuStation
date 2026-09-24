// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Common.Chat;
using Content.Omu.Shared.Traits;
using Content.Server.Chat.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Traits;

public sealed class DamagedThroatSystem : SharedDamagedThroatSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamagedThroatComponent, EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<DamagedThroatComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<DamagedThroatComponent, EmoteSoundVolumeShiftEvent>(OnEmoteSoundVolumeShift);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DamagedThroatComponent>();
        while (query.MoveNext(out var uid, out var throat))
        {
            if (throat.NextCoughAt is not { } coughAt || now < coughAt)
                continue;

            throat.NextCoughAt = null;
            _chat.TryEmoteWithChat(uid, throat.CoughEmote.Id);
        }
    }

    private void OnSpoke(Entity<DamagedThroatComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.IsWhisper || !args.Language.SpeechOverride.RequireSpeech)
            return;

        StrainThroat(ent);
    }

    private void OnEmote(Entity<DamagedThroatComponent> ent, ref EmoteEvent args)
    {
        if (args.Emote.ID == ent.Comp.ScreamEmote.Id)
            StrainThroat(ent);
    }

    private void OnEmoteSoundVolumeShift(Entity<DamagedThroatComponent> ent, ref EmoteSoundVolumeShiftEvent args)
    {
        if (args.EmoteId == ent.Comp.ScreamEmote.Id)
            return;

        if (!_prototype.TryIndex<EmotePrototype>(args.EmoteId, out var emote) || !emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        args.Volume += ent.Comp.VocalEmoteVolumeShift;
    }

    private void StrainThroat(Entity<DamagedThroatComponent> ent)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.LastStrainedAt + ent.Comp.StrainCooldown)
            return;

        if (now >= ent.Comp.LastStrainedAt + ent.Comp.RecoveryTime)
            ent.Comp.Escalation = 0;

        ent.Comp.LastStrainedAt = now;

        var multiplier = 1f + Math.Min(ent.Comp.Escalation, ent.Comp.MaxEscalation);
        ent.Comp.Escalation++;

        _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage * multiplier, interruptsDoAfters: false, targetPart: TargetBodyPart.Chest, canMiss: false);

        ent.Comp.NextCoughAt = now + _random.Next(ent.Comp.MinCoughDelay, ent.Comp.MaxCoughDelay);
    }
}

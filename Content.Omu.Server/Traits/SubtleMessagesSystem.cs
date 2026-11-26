// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Server.Arcade;
using Content.Omu.Server.StationAI;
using Content.Shared.Random.Helpers;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Traits;

public sealed class SubtleMessagesSystem : EntitySystem
{
    [Dependency] private readonly PolybiusAttackEffectsSystem _attackEffects = default!;
    [Dependency] private readonly StationAiEyePopupSystem _eyePopup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SubtleMessagesComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SubtleMessagesComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextMessageTime = _timing.CurTime
            + _random.Next(ent.Comp.MinTimeBetweenMessages, ent.Comp.MaxTimeBetweenMessages);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<SubtleMessagesComponent>();
        while (query.MoveNext(out var uid, out var messages))
        {
            if (messages.NextMessageTime > _timing.CurTime)
                continue;

            messages.NextMessageTime = _timing.CurTime
                + _random.Next(messages.MinTimeBetweenMessages, messages.MaxTimeBetweenMessages);

            if (_attackEffects.IsShowingAttackLines(uid))
                continue;

            var dataset = HasComp<StationAiHeldComponent>(uid)
                ? messages.StationAiMessages ?? messages.Messages
                : messages.Messages;

            var message = _random.Pick(_prototype.Index(dataset));
            _eyePopup.PopupToSelf(message, uid);
        }
    }
}

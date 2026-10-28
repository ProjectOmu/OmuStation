// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.MartialArts.Events;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Robust.Shared.Player;

namespace Content.Omu.Server.MartialArts;

public sealed class WrestlingCatchphraseSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, WrestlingCatchphraseEvent>(OnCatchphrase);
    }

    private void OnCatchphrase(Entity<ActorComponent> ent, ref WrestlingCatchphraseEvent args)
    {
        _chat.TrySendInGameICMessage(ent, args.Message, InGameICChatType.Speak, false);
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Emoting;
using Content.Shared.Speech.Components;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Speech.EntitySystems;

public sealed class OmuVocalSystem : EntitySystem
{
    private static readonly ProtoId<EmoteSoundsPrototype> FallbackSounds = "VocalFallback";

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmotingComponent, EmoteEvent>(OnEmote, after: [typeof(VocalSystem)]);
    }

    private void OnEmote(Entity<EmotingComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.Vocal) || !HasComp<VocalComponent>(ent))
            return;

        args.Handled = _chat.TryPlayEmoteSound(ent.Owner, _prototype.Index(FallbackSounds), args.Emote);
    }
}

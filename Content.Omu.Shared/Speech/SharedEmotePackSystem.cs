// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Omu.Speech;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Speech;

public abstract class SharedEmotePackSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmotePackComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<EmotePackComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpeechComponent>(ent, out var speech)
            || !TryGetSource(ent.Comp, out _, out var source)
            || !source.TryGetComponent<SpeechComponent>(out var sourceSpeech, _factory))
            return;

        foreach (var emote in sourceSpeech.AllowedEmotes)
        {
            if (!speech.AllowedEmotes.Contains(emote))
                speech.AllowedEmotes.Add(emote);
        }
    }

    protected bool TryGetSource(EmotePackComponent comp,
        [NotNullWhen(true)] out EmotePackPrototype? pack,
        [NotNullWhen(true)] out EntityPrototype? source)
    {
        source = EmotePackRules.SpeciesMob(_proto, comp.Species);
        return _proto.TryIndex(comp.Pack, out pack) && source != null;
    }
}

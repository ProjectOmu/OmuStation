// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Server.Speech.Components;
using Content.Server.Speech;
using Content.Shared.Chat;

namespace Content.Omu.Server.Speech.EntitySystems;

public sealed class SpeechReplacementRestoreSystem : EntitySystem
{
    [Dependency] private readonly SpeechReplacementSystem _speechReplacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech, after: [typeof(AccentSystem)]);
    }

    private void OnTransformSpeech(TransformSpeechEvent args)
    {
        if (_speechReplacement.TryGetReplacements(args.Sender, out _))
            args.Message = args.Message.Replace(SpeechReplacementComponent.Shield, string.Empty);
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Text.RegularExpressions;
using Content.Omu.Server.Speech.Components;
using Content.Server.Chat.Systems;
using Content.Server.Speech;
using Content.Server.VoiceMask;
using Content.Shared.Chat;
using Content.Shared.Cloning.Events;
using Content.Shared.GameTicking;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Speech.EntitySystems;

public sealed class SpeechReplacementSystem : EntitySystem
{
    private static readonly Regex WordRegex = new(@"[\w']+");

    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<SpeechReplacementComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SpeechReplacementComponent, CloningEvent>(OnCloning);
        SubscribeLocalEvent<SpeechReplacementComponent, TransformSpeechEvent>(OnTransformSpeech,
            before: [typeof(AccentSystem)],
            after: [typeof(VoiceMaskSystem)]);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Profile.SpeechReplacements.Count == 0)
            return;

        AddComp(ev.Mob, new SpeechReplacementComponent { Replacements = new(ev.Profile.SpeechReplacements) }, true);
    }

    private void OnInit(Entity<SpeechReplacementComponent> ent, ref ComponentInit args)
    {
        var comp = ent.Comp;
        comp.Lookup.Clear();
        foreach (var replacement in comp.Replacements)
        {
            var text = FormattedMessage.EscapeText(_chat.SanitizeMessageReplaceWords(replacement.Replacement))
                .Replace(SpeechReplacementComponent.Shield, string.Empty);
            if (replacement.Word.Length > 0 && text.Length > 0)
                comp.Lookup.TryAdd(replacement.Word, text);
        }

        if (comp.Lookup.Count == 0)
        {
            comp.Pattern = null;
            return;
        }

        var words = comp.Lookup.Keys.OrderByDescending(word => word.Length).Select(Regex.Escape);
        comp.Pattern = new Regex($@"(?<![\w'])(?:{string.Join('|', words)})(?![\w'])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private void OnCloning(Entity<SpeechReplacementComponent> ent, ref CloningEvent args)
    {
        AddComp(args.CloneUid, new SpeechReplacementComponent { Replacements = new(ent.Comp.Replacements) }, true);
    }

    private void OnTransformSpeech(Entity<SpeechReplacementComponent> ent, ref TransformSpeechEvent args)
    {
        if (args.Cancelled || ent.Comp.Pattern is not { } pattern)
            return;

        var message = args.Message.Replace(SpeechReplacementComponent.Shield, string.Empty);
        args.Message = pattern.Replace(message, match => Substitute(ent.Comp, match));
    }

    private static string Substitute(SpeechReplacementComponent comp, Match match)
    {
        var word = match.Value;
        if (!comp.Lookup.TryGetValue(word, out var replacement))
            return word;

        if (word.Any(char.IsUpper) && !word.Any(char.IsLower) && (word.Length > 1 || replacement.Length == 1))
            replacement = replacement.ToUpperInvariant();
        else if (char.IsUpper(word[0]))
            replacement = char.ToUpperInvariant(replacement[0]) + replacement[1..];

        return WordRegex.Replace(replacement,
            run => SpeechReplacementComponent.Shield + run.Value + SpeechReplacementComponent.Shield);
    }
}

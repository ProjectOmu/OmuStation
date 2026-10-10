// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Omu.Common.Changeling;
using Content.Omu.Server.Speech.Components;
using Content.Server.Chat.Systems;
using Content.Server.Speech;
using Content.Server.VoiceMask;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Cloning.Events;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Speech.EntitySystems;

public sealed class SpeechReplacementSystem : EntitySystem
{
    private static readonly Regex WordRegex = new(@"[\w']+");

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    private int _maxMessageLength;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.ChatMaxMessageLength, x => _maxMessageLength = x, true);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<SpeechReplacementComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SpeechReplacementComponent, CloningEvent>(OnCloning);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ChangelingFormAssumedEvent>(OnFormAssumed);
        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech,
            before: [typeof(AccentSystem)],
            after: [typeof(VoiceMaskSystem)]);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Profile.SpeechReplacements.Count == 0)
            return;

        AddComp(ev.Mob, new SpeechReplacementComponent { Replacements = new(ev.Profile.SpeechReplacements) }, true);
        if (_mind.TryGetMind(ev.Mob, out var mindId, out _))
            AddComp(mindId, new SpeechReplacementComponent { Replacements = new(ev.Profile.SpeechReplacements) }, true);
    }

    public bool TryGetReplacements(EntityUid speaker, [NotNullWhen(true)] out SpeechReplacementComponent? comp)
    {
        if (TryComp(speaker, out comp))
            return true;

        return _mind.TryGetMind(speaker, out var mindId, out _) && TryComp(mindId, out comp);
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

    private void OnFormAssumed(Entity<HumanoidAppearanceComponent> ent, ref ChangelingFormAssumedEvent args)
    {
        var replacements = TryGetReplacements(args.Original, out var original) ? original.Replacements : [];
        AddComp(ent, new SpeechReplacementComponent { Replacements = new(replacements) }, true);
    }

    private void OnTransformSpeech(TransformSpeechEvent args)
    {
        if (args.Cancelled || !TryGetReplacements(args.Sender, out var comp) || comp.Pattern is not { } pattern)
            return;

        var message = args.Message.Replace(SpeechReplacementComponent.Shield, string.Empty);
        var room = _maxMessageLength - message.Length;
        args.Message = pattern.Replace(message, match =>
        {
            if (!comp.Lookup.TryGetValue(match.Value, out var replacement)
                || replacement.Length - match.Length > room)
                return match.Value;

            room -= replacement.Length - match.Length;
            return Substitute(match.Value, replacement);
        });
    }

    private static string Substitute(string word, string replacement)
    {
        if (word.Any(char.IsUpper) && !word.Any(char.IsLower) && (word.Length > 1 || replacement.Length == 1))
            replacement = replacement.ToUpperInvariant();
        else if (char.IsUpper(word[0]))
            replacement = char.ToUpperInvariant(replacement[0]) + replacement[1..];

        return WordRegex.Replace(replacement,
            run => SpeechReplacementComponent.Shield + run.Value + SpeechReplacementComponent.Shield);
    }
}

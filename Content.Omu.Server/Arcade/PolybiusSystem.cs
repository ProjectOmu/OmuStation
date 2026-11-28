// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Server.StationAI;
using Content.Omu.Shared.Arcade;
using Content.Shared.Chat;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Arcade;

public sealed class PolybiusSystem : EntitySystem
{
    private static readonly Regex GameName =
        new(@"(polybius)('s)?(\.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly PolybiusAttackEffectsSystem _attackEffects = default!;
    [Dependency] private readonly StationAiEyePopupSystem _eyePopup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PolybiusArcadeComponent, AfterActivatableUIOpenEvent>(OnArcadeOpened);
        SubscribeLocalEvent<PolybiusArcadeComponent, PolybiusArcadePlayMessage>(OnArcadePlay);
        SubscribeLocalEvent<PolybiusArcadeComponent, EntitySpokeEvent>(OnArcadeSpoke);
        SubscribeLocalEvent<PolybiusForgottenComponent, TransformSpeechEvent>(OnForgottenSpeech);
        SubscribeLocalEvent<PolybiusStatusEffectComponent, StatusEffectAppliedEvent>(OnStatusEffectApplied);
        SubscribeLocalEvent<PolybiusStatusEffectComponent, StatusEffectRemovedEvent>(OnStatusEffectRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<PolybiusArcadeComponent>();
        while (query.MoveNext(out var uid, out var arcade))
        {
            if (arcade.SpiralEndTime is not { } endTime || endTime > _timing.CurTime)
                continue;

            arcade.SpiralEndTime = null;
            _appearance.SetData(uid, PolybiusArcadeVisuals.Spiral, false);
        }
    }

    private void OnArcadeOpened(Entity<PolybiusArcadeComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        _statusEffects.TrySetStatusEffectDuration(args.User, ent.Comp.StatusEffect, ent.Comp.Duration);
        EnsureComp<PolybiusForgottenComponent>(args.User);
    }

    private void OnForgottenSpeech(Entity<PolybiusForgottenComponent> ent, ref TransformSpeechEvent args)
    {
        if (!GameName.IsMatch(args.Message))
            return;

        args.Message = GameName.Replace(args.Message, match => FitReplacement(match, PickForgottenWord(ent.Comp)));
        _eyePopup.PopupToSelf(Loc.GetString(ent.Comp.Popup), ent);
    }

    private string PickForgottenWord(PolybiusForgottenComponent forgotten)
    {
        var pool = _random.Prob(0.5f) ? forgotten.Names : forgotten.TrailOffs;
        var options = _prototype.Index(pool).Values.Where(word => word != forgotten.LastWord).ToList();
        forgotten.LastWord = _random.Pick(options);
        return Loc.GetString(forgotten.LastWord);
    }

    private static string FitReplacement(Match match, string replacement)
    {
        var name = match.Groups[1].Value;
        var text = replacement.EndsWith("...")
            ? replacement
            : replacement + match.Groups[2].Value + match.Groups[3].Value;

        if (name == name.ToUpperInvariant())
            return text.ToUpperInvariant();

        return match.Index == 0 ? char.ToUpperInvariant(text[0]) + text[1..] : text;
    }

    private void OnArcadePlay(Entity<PolybiusArcadeComponent> ent, ref PolybiusArcadePlayMessage args)
    {
        _eyePopup.PopupToSelf(Loc.GetString(ent.Comp.PlayPopup), args.Actor);
        _ui.CloseUi(ent.Owner, args.UiKey, args.Actor);
    }

    private void OnArcadeSpoke(Entity<PolybiusArcadeComponent> ent, ref EntitySpokeEvent args)
    {
        ent.Comp.SpiralEndTime = _timing.CurTime + ent.Comp.SpiralDuration;
        _appearance.SetData(ent, PolybiusArcadeVisuals.Spiral, true);
    }

    private void OnStatusEffectApplied(Entity<PolybiusStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        foreach (var (name, entry) in ent.Comp.Components)
        {
            if (entry.Component is NarcolepsyComponent && _attackEffects.UsesFreezeAttacks(args.Target))
                continue;

            if (!HasComp(args.Target, entry.Component.GetType()))
                ent.Comp.Granted.Add(name, entry);
        }

        EntityManager.AddComponents(args.Target, ent.Comp.Granted);
    }

    private void OnStatusEffectRemoved(Entity<PolybiusStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        EntityManager.RemoveComponents(args.Target, ent.Comp.Granted);
    }
}

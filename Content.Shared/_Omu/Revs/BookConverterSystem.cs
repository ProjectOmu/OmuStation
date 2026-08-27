using Content.Shared._EinsteinEngines.Language.Components;
using Content.Shared._EinsteinEngines.Language.Systems;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.DoAfter;
using Content.Shared.Flash;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Revolutionary.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;


// Basically a copy of the EE book revs but with bits ripped out and other bits mashed in.
namespace Content.Shared._Omu.Revs;

public sealed class BookConverterSystem : EntitySystem
{
    private static readonly ProtoId<LocalizedDatasetPrototype> RevConvertSpeechProto = "RevolutionaryConverterSpeech";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedLanguageSystem _language = default!;
    [Dependency] private readonly SharedChargesSystem _chargesSystem = default!;

    private LocalizedDatasetPrototype? _speechLocalization;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BookConverterComponent, RevolutionaryConverterDoAfterEvent>(OnConvertDoAfter);
        SubscribeLocalEvent<BookConverterComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<BookConverterComponent, AfterInteractEvent>(OnConverterAfterInteract);

        _speechLocalization = _prototypeManager.Index<LocalizedDatasetPrototype>(RevConvertSpeechProto);
    }

    public void OnConvertDoAfter(Entity<BookConverterComponent> ent, ref RevolutionaryConverterDoAfterEvent args)
    {
        if (args.Target == null
            || args.Cancelled
            || args.Used == null
            || args.Target == null)
            return;

        var ev = new MoraleChangedArgs();
        ev.Amount = ent.Comp.Amount * ent.Comp.FocusedMultiplier;
        ev.User = args.User;
        ev.Forced = true;
        RaiseLocalEvent(args.Target.Value, ev);
    }
    private void OnUseInHand(Entity<BookConverterComponent> ent, ref UseInHandEvent args)
    {
        if (!SpeakPropaganda(ent, args.User))
            return;

        if (HasComp<HeadRevolutionaryComponent>(args.User))
        {
            var ev = new BookConverterUsedEvent(args.User, ent.Comp.Amount, ent.Comp.Range);
            RaiseLocalEvent(args.User, ref ev);
        }

        args.Handled = true;
    }

    private bool SpeakPropaganda(Entity<BookConverterComponent> conversionToolEntity, EntityUid user)
    {
        if(_speechLocalization == null
            || _speechLocalization.Values.Count == 0
            || conversionToolEntity.Comp.Silent)
            return false;

        var message = _random.Pick(_speechLocalization);
        _chat.TrySendInGameICMessage(user, Loc.GetString(message), InGameICChatType.Speak, hideChat: false, hideLog: false);
        return true;
    }

    public void OnConverterAfterInteract(Entity<BookConverterComponent> entity, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.Target.HasValue
            || !args.CanReach)
            return;

        if (args.Target is not { Valid: true } target
            || !HasComp<MobStateComponent>(target)
            || !HasComp<HeadRevolutionaryComponent>(args.User))
            return;

        ConvertDoAfter(entity, target, args.User);
    }

    private void ConvertDoAfter(Entity<BookConverterComponent> converter, EntityUid target, EntityUid user)
    {
        if (user == target)
            return;

        if (SpeakPropaganda(converter, user)
            // Note: this check is skipped if the speaker speaks lines and somehow doesn't have a languageSpeaker component.
            && EntityManager.TryGetComponent<LanguageSpeakerComponent>(user, out var speakerComponent)) // returns true if the chosen conversion method uses a spoken line of text
        {
            //check if spoken language can be understood by target
            if (!_language.CanUnderstand(target, speakerComponent.CurrentLanguage))
                return; //the target does not understand the speaker's language, so the conversion fails
        }

        if (converter.Comp.ConversionDuration > TimeSpan.Zero)
        {
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
                user,
                converter.Comp.ConversionDuration,
                new RevolutionaryConverterDoAfterEvent(),
                converter.Owner,
                target: target,
                used: converter.Owner,
                showTo: user)
            {
                Hidden = !converter.Comp.VisibleDoAfter,
                BreakOnMove = false,
                BreakOnWeightlessMove = false,
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnHandChange = false,
            });
        }
        else
        {
            var ev = new MoraleChangedArgs();
            ev.Amount = converter.Comp.Amount * converter.Comp.FocusedMultiplier;
            ev.User = user;
            ev.Forced = true;
            RaiseLocalEvent(target, ev);
        }
    }
}

/// <summary>
/// Called after a converter is used on another person to check for rev conversion.
/// Raised on the user of the converter, the target hit by the converter, and the converter used.
/// </summary>
[ByRefEvent]
public readonly struct AfterRevolutionaryConvertedEvent(EntityUid target, EntityUid? user, EntityUid? used)
{
    public readonly EntityUid Target = target;
    public readonly EntityUid? User = user;
    public readonly EntityUid? Used = used;
}

[ByRefEvent]
public record struct BookConverterUsedEvent(EntityUid User, float Change, float range);

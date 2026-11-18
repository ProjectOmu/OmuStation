// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Common.Paper;
using Content.Shared.Access.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.Traits;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Paper;

public sealed class SignatureFontSystem : CommonSignatureFontSystem
{
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignatureFontSelectorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SignatureFontSelectorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        Subs.BuiEvents<SignatureFontSelectorComponent>(SignatureFontUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnOpened);
                subs.Event<SignatureFontSelectMessage>(OnSelect);
            });
    }

    public override string? GetFont(EntityUid signer, EntityUid pen)
    {
        return CompOrNull<SignatureFontSelectorComponent>(pen)?.Font ?? GetOwnFont(signer);
    }

    public override string? GetTraitFont(ComponentRegistry components)
    {
        if (!components.TryGetValue(Factory.GetComponentName<SignatureFontComponent>(), out var entry))
            return null;

        return ((SignatureFontComponent) entry.Component).Font;
    }

    public IEnumerable<(TraitPrototype Trait, string Font)> EnumerateFontTraits()
    {
        foreach (var trait in _prototype.EnumeratePrototypes<TraitPrototype>())
        {
#pragma warning disable CS0618
            var components = trait.Components;
#pragma warning restore CS0618
            if (components != null && GetTraitFont(components) is { } font)
                yield return (trait, font);
        }
    }

    private string? GetOwnFont(EntityUid signer)
    {
        if (CompOrNull<SignatureFontComponent>(signer)?.Font is { } font)
            return font;

        var species = CompOrNull<HumanoidAppearanceComponent>(signer)?.Species;

        foreach (var rule in _prototype.EnumeratePrototypes<SignatureFontDefaultPrototype>())
        {
            if ((species is { } id && rule.Species.Contains(id)) || _whitelist.IsWhitelistPass(rule.Whitelist, signer))
                return rule.Font;
        }

        return null;
    }

    private void OnMapInit(Entity<SignatureFontSelectorComponent> ent, ref MapInitEvent args)
    {
        _ui.SetUi(ent.Owner, SignatureFontUiKey.Key, new InterfaceData("SignatureFontBoundUserInterface"));
    }

    private void OnGetVerbs(Entity<SignatureFontSelectorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("signature-font-selector-verb"),
            Act = () => _ui.OpenUi(ent.Owner, SignatureFontUiKey.Key, user),
        });
    }

    private void OnOpened(Entity<SignatureFontSelectorComponent> ent, ref BoundUIOpenedEvent args)
    {
        var name = _idCard.TryFindIdCard(args.Actor, out var id) && !string.IsNullOrWhiteSpace(id.Comp.FullName)
            ? id.Comp.FullName
            : Name(args.Actor);

        var state = new SignatureFontBoundUserInterfaceState(name, GetOwnFont(args.Actor) ?? "Default", ent.Comp.Font);
        _ui.SetUiState(ent.Owner, SignatureFontUiKey.Key, state);
    }

    private void OnSelect(Entity<SignatureFontSelectorComponent> ent, ref SignatureFontSelectMessage args)
    {
        if (args.Font == null)
        {
            ent.Comp.Font = null;
            _popup.PopupEntity(Loc.GetString("signature-font-selector-reset"), args.Actor, args.Actor);
            _ui.CloseUi(ent.Owner, SignatureFontUiKey.Key, args.Actor);
            return;
        }

        foreach (var (trait, font) in EnumerateFontTraits())
        {
            if (font != args.Font)
                continue;

            ent.Comp.Font = font;
            var message = Loc.GetString("signature-font-selector-set", ("font", Loc.GetString(trait.Name)));
            _popup.PopupEntity(message, args.Actor, args.Actor);
            _ui.CloseUi(ent.Owner, SignatureFontUiKey.Key, args.Actor);
            return;
        }
    }
}

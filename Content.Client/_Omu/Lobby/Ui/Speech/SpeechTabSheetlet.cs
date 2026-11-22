// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Omu.Lobby.Ui.Speech;

[CommonSheetlet]
public sealed class SpeechTabSheetlet : Sheetlet<PalettedStylesheet>
{
    public const string CardClass = "SpeechReplacementCard";
    private const string BadgeClass = "SpeechReplacementBadge";

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var card = new StyleBoxFlat
        {
            BackgroundColor = sheet.SecondaryPalette.Background,
            BorderColor = sheet.SecondaryPalette.BackgroundLight,
            BorderThickness = new Thickness(1),
        };
        card.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);
        card.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);

        var badge = new StyleBoxFlat { BackgroundColor = sheet.HighlightPalette.Background };
        badge.SetContentMarginOverride(StyleBox.Margin.Horizontal, 6);
        badge.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);

        var badgeFull = new StyleBoxFlat { BackgroundColor = sheet.NegativePalette.Background };
        badgeFull.SetContentMarginOverride(StyleBox.Margin.Horizontal, 6);
        badgeFull.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);

        return
        [
            E<PanelContainer>().Class(CardClass).Panel(card),
            E<PanelContainer>().Class(BadgeClass).Panel(badge),
            E<PanelContainer>().Class(BadgeClass).Class(StyleClass.Negative).Panel(badgeFull),
        ];
    }
}

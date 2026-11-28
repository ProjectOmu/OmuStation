// SPDX-License-Identifier: MIT

using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Omu.Client.Billiards.UI;

[CommonSheetlet]
public sealed class BilliardsSheetlet : Sheetlet<PalettedStylesheet>
{
    private const string StyleClassCallout = "BilliardsCallout";
    private const string StyleClassMenu = "BilliardsMenu";

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var plate = Box(sheet.SecondaryPalette.Background, sheet.SecondaryPalette.BackgroundLight, 1, 6, 6);
        var plateActive = Box(sheet.SecondaryPalette.Background, sheet.PositivePalette.Element, 2, 6, 6);
        var portrait = Box(sheet.SecondaryPalette.BackgroundDark, sheet.SecondaryPalette.BackgroundLight, 1, 2, 2);
        var callout = Box(sheet.SecondaryPalette.BackgroundDark, sheet.HighlightPalette.Base, 2, 24, 10);
        var life = Box(sheet.NegativePalette.Element, sheet.NegativePalette.PressedElement, 1, 0, 0);
        var lifeLost = Box(sheet.SecondaryPalette.BackgroundDark, sheet.SecondaryPalette.BackgroundLight, 1, 0, 0);
        var menu = Box(sheet.SecondaryPalette.Background, sheet.HighlightPalette.Base, 2, 16, 12);

        return
        [
            E<PanelContainer>().Class(BilliardsPlate.StyleClassPlate).Panel(plate),
            E<PanelContainer>().Class(BilliardsPlate.StyleClassPlateActive).Panel(plateActive),
            E<PanelContainer>().Class(BilliardsPlate.StyleClassPortrait).Panel(portrait),
            E<PanelContainer>().Class(BilliardsPlate.StyleClassLife).Panel(life),
            E<PanelContainer>().Class(BilliardsPlate.StyleClassLifeLost).Panel(lifeLost),
            E<PanelContainer>().Class(StyleClassCallout).Panel(callout),
            E<PanelContainer>().Class(StyleClassMenu).Panel(menu),
        ];
    }

    private static StyleBoxFlat Box(Color fill, Color border, float thickness, float horizontal, float vertical)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = fill,
            BorderColor = border,
            BorderThickness = new Thickness(thickness),
        };
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, horizontal);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, vertical);
        return box;
    }
}

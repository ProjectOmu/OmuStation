// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Omu.Lobby.Ui;

[CommonSheetlet]
public sealed class AlertTabContainerSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var alertActive = new StyleBoxFlat(sheet.NegativePalette.Element);
        var alertInactive = new StyleBoxFlat(sheet.NegativePalette.Background);

        return
        [
            E<AlertTabContainer>()
                .Prop(AlertTabContainer.StylePropertyAlertTabStyleBox, alertActive)
                .Prop(AlertTabContainer.StylePropertyAlertTabStyleBoxInactive, alertInactive)
                .Prop(AlertTabContainer.StylePropertyAlertTabFontColor, Color.White),
        ];
    }
}

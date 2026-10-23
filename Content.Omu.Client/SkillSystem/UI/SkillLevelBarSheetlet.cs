// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Omu.Client.SkillSystem.UI;

[CommonSheetlet]
public sealed class SkillLevelBarSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        return
        [
            E<SkillLevelBar>()
                .Prop(SkillLevelBar.StylePropertyTrack, sheet.SecondaryPalette.BackgroundDark)
                .Prop(SkillLevelBar.StylePropertyBorder, sheet.SecondaryPalette.BackgroundLight),
        ];
    }
}

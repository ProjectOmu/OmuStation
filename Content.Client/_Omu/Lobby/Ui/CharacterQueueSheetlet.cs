// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Colorspace;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Omu.Lobby.Ui;

[CommonSheetlet]
public sealed class CharacterQueueSheetlet : Sheetlet<PalettedStylesheet>
{
    private const string ActiveClass = "CharacterQueueActive";
    private const string DeleteClass = "CharacterQueueDelete";

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var primary = sheet.PrimaryPalette;
        var negative = sheet.NegativePalette;
        var raised = primary.HoveredElement.NudgeLightness(primary.LightnessShift);

        StyleRule[] Shades(string styleClass, Color hovered, Color pressed) =>
        [
            Shade(QueueButton(styleClass).PseudoNormal(), raised),
            Shade(QueueButton(styleClass).PseudoHovered(), hovered),
            Shade(QueueButton(styleClass).PseudoPressed(), pressed),
            Shade(QueueButton(styleClass).PseudoDisabled(), primary.DisabledElement),
        ];

        return
        [
            ..Shades(ActiveClass, raised.NudgeLightness(primary.LightnessShift), sheet.PositivePalette.HoveredElement),
            ..Shades(DeleteClass, negative.HoveredElement, negative.PressedElement),
        ];
    }

    private static MutableSelectorElement QueueButton(string styleClass)
    {
        return E<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(styleClass);
    }

    private static MutableSelector Shade(MutableSelector selector, Color color)
    {
        return selector.Prop(Control.StylePropertyModulateSelf, color);
    }
}

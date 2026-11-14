// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Omu.Lobby.Ui;

public sealed class AlertTabContainer : TabContainer
{
    public const string StylePropertyAlertTabStyleBox = "alert-tab-stylebox";
    public const string StylePropertyAlertTabStyleBoxInactive = "alert-tab-stylebox-inactive";
    public const string StylePropertyAlertTabFontColor = "alert-tab-font-color";

    private static readonly AttachedProperty<bool> TabAlertProperty =
        AttachedProperty<bool>.Create("TabAlert", typeof(AlertTabContainer));

    public static void SetTabAlert(Control control, bool alert)
    {
        control.SetValue(TabAlertProperty, alert);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var font = TryGetStyleProperty<Font>(Label.StylePropertyFont, out var styleFont)
            ? styleFont
            : UserInterfaceManager.ThemeDefaults.DefaultFont;
        var offset = 0f;

        for (var i = 0; i < ChildCount; i++)
        {
            if (!GetTabVisible(i))
                continue;

            var active = CurrentTab == i;
            var title = GetActualTabTitle(i);
            var topLeft = new Vector2(offset, 0);
            var size = new Vector2(handle.GetDimensions(font, title, UIScale).X, font.GetHeight(UIScale));
            TryGetStyleProperty<StyleBox>(active ? StylePropertyTabStyleBox : StylePropertyTabStyleBoxInactive,
                out var box);
            var header = box?.GetEnvelopBox(topLeft, size, UIScale) ?? UIBox2.FromDimensions(topLeft, size);
            offset += header.Width;

            var alertProperty = active ? StylePropertyAlertTabStyleBox : StylePropertyAlertTabStyleBoxInactive;
            if (!GetChild(i).GetValue(TabAlertProperty) ||
                !TryGetStyleProperty<StyleBox>(alertProperty, out var alertBox) ||
                !TryGetStyleProperty(StylePropertyAlertTabFontColor, out Color fontColor))
                continue;

            alertBox.Draw(handle, header, UIScale);
            handle.DrawString(font, (box?.GetContentBox(header, UIScale) ?? header).TopLeft, title, UIScale, fontColor);
        }
    }
}

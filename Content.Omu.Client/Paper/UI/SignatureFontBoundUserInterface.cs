// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Paper;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Paper.UI;

public sealed class SignatureFontBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SignatureFontMenu? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SignatureFontMenu>();
        _window.OnFontSelected += font => SendMessage(new SignatureFontSelectMessage(font));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SignatureFontBoundUserInterfaceState current)
            _window?.UpdateState(current);
    }
}

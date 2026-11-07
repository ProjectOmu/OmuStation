// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Library;

[UsedImplicitly]
public sealed class BookPinpointerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private BookPinpointerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BookPinpointerWindow>();
        _window.OnSelect += book => SendMessage(new BookPinpointerSelectMessage(book));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BookPinpointerBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Library;

[UsedImplicitly]
public sealed class LibraryRequestConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private LibraryRequestConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<LibraryRequestConsoleWindow>();
        _window.OnSearch += (title, author, page) => SendMessage(new LibraryRequestSearchMessage(title, author, page));
        _window.OnRequestArchive += id => SendMessage(new LibraryRequestArchiveMessage(id));
        _window.OnRequestBlank += (title, cover) => SendMessage(new LibraryRequestBlankMessage(title, cover));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is LibraryRequestConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}

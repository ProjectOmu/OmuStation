// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Library;

[UsedImplicitly]
public sealed class LibraryScannerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private LibraryScannerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<LibraryScannerWindow>();
        _window.OnScan += () => SendMessage(new LibraryScannerScanMessage());
        _window.OnClear += () => SendMessage(new LibraryScannerClearMessage());
        _window.OnEject += () => SendMessage(new LibraryScannerEjectMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is LibraryScannerBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}

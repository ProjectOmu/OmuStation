// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Omu.Shared.Library;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Omu.Client.Library;

/// <summary>
///     The library scanner window: scan the inserted book, clear the cache or eject the book.
///     Ported from tgstation's LibraryScanner tgui interface.
/// </summary>
public sealed class LibraryScannerWindow : DefaultWindow
{
    public event Action? OnScan;
    public event Action? OnClear;
    public event Action? OnEject;

    private readonly Label _bookLabel = new();
    private readonly Label _cacheLabel = new() { ClipText = true };
    private readonly Button _scan = new() { Text = Loc.GetString("library-scanner-ui-scan"), HorizontalExpand = true };
    private readonly Button _clear = new() { Text = Loc.GetString("library-scanner-ui-clear"), HorizontalExpand = true };
    private readonly Button _eject = new() { Text = Loc.GetString("library-scanner-ui-eject"), HorizontalExpand = true };

    public LibraryScannerWindow()
    {
        Title = Loc.GetString("library-scanner-ui-title");
        MinSize = new Vector2(360, 160);

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            Children =
            {
                _bookLabel,
                _cacheLabel,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { _scan, _clear, _eject },
                },
            },
        });

        _scan.OnPressed += _ => OnScan?.Invoke();
        _clear.OnPressed += _ => OnClear?.Invoke();
        _eject.OnPressed += _ => OnEject?.Invoke();
    }

    public void UpdateState(LibraryScannerBoundUserInterfaceState state)
    {
        _bookLabel.Text = state.HasBook
            ? Loc.GetString("library-scanner-ui-has-book")
            : Loc.GetString("library-scanner-ui-no-book");

        _cacheLabel.Text = state.HasCache
            ? Loc.GetString("library-scanner-ui-cached",
                ("title", state.CacheTitle),
                ("author", string.IsNullOrWhiteSpace(state.CacheAuthor)
                    ? Loc.GetString("library-ui-unknown")
                    : state.CacheAuthor))
            : Loc.GetString("library-scanner-ui-no-cache");

        _scan.Disabled = !state.CanScan;
        _clear.Disabled = !state.HasCache;
        _eject.Disabled = !state.HasBook;
    }
}

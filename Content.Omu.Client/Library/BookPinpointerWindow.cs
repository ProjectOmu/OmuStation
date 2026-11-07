// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Omu.Shared.Library;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Omu.Client.Library;

/// <summary>
///     The menu of a book pinpointer: the books to choose from, with a search box. Picking one starts the tracking.
/// </summary>
public sealed class BookPinpointerWindow : DefaultWindow
{
    /// <summary>Rows shown at once, a station can have hundreds of books. Searching narrows the list.</summary>
    private const int MaxRows = 100;

    public event Action<NetEntity>? OnSelect;

    private readonly OptionButton _mode = new();
    private readonly Label _header = new() { ClipText = true };
    private readonly LineEdit _search = new() { PlaceHolder = Loc.GetString("library-pinpointer-ui-search"), HorizontalExpand = true };
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _more = new() { FontColorOverride = Color.Gray };
    private BookPinpointerBoundUserInterfaceState? _state;

    public BookPinpointerWindow()
    {
        Title = Loc.GetString("library-pinpointer-ui-title");
        MinSize = new Vector2(360, 360);
        SetSize = new Vector2(420, 460);

        _search.OnTextChanged += _ => Rebuild();
        _mode.AddItem(Loc.GetString("library-pinpointer-ui-mode-all"), 0);
        _mode.AddItem(Loc.GetString("library-pinpointer-ui-mode-missing"), 1);
        _mode.AddItem(Loc.GetString("library-pinpointer-ui-mode-overdue"), 2);
        _mode.OnItemSelected += args =>
        {
            _mode.SelectId(args.Id);
            UpdateHeader();
            Rebuild();
        };
        _mode.Visible = false;

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true,
            Margin = new Thickness(4),
            Children =
            {
                _mode,
                _header,
                _search,
                new ScrollContainer
                {
                    VerticalExpand = true,
                    HorizontalExpand = true,
                    HScrollEnabled = false,
                    Children = { _list },
                },
                _more,
            },
        });
    }

    public void UpdateState(BookPinpointerBoundUserInterfaceState state)
    {
        _state = state;

        // Only the universal pinpointer knows about missing books.
        _mode.Visible = state.Universal;
        if (!state.Universal)
            _mode.SelectId(0);

        UpdateHeader();
        Rebuild();
    }

    private bool ShowingMissing => _mode.SelectedId == 1;
    private bool ShowingOverdue => _mode.SelectedId == 2;

    private void UpdateHeader()
    {
        if (_state == null)
            return;

        _header.Text = ShowingOverdue
            ? Loc.GetString("library-pinpointer-ui-header-overdue", ("count", _state.Overdue.Count))
            : ShowingMissing
            ? Loc.GetString("library-pinpointer-ui-header-missing", ("count", _state.Missing.Count))
            : Loc.GetString(_state.Universal
                ? "library-pinpointer-ui-header-station"
                : "library-pinpointer-ui-header-room", ("count", _state.Books.Count));
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        _more.Text = string.Empty;

        if (_state == null)
            return;

        var filter = _search.Text.Trim();
        var matches = 0;
        var shown = 0;

        foreach (var book in ShowingOverdue ? _state.Overdue : ShowingMissing ? _state.Missing : _state.Books)
        {
            if (filter.Length > 0 &&
                !book.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !book.Author.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches++;
            if (shown >= MaxRows)
                continue;

            shown++;
            var track = new Button { Text = Loc.GetString("library-pinpointer-ui-track") };
            var netEntity = book.Book;
            track.OnPressed += _ => OnSelect?.Invoke(netEntity);

            _list.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children =
                {
                    new Label { Text = book.Borrower.Length > 0 ? Loc.GetString("library-pinpointer-ui-borrowed-by", ("title", book.Title), ("borrower", book.Borrower)) : book.Title, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 3 },
                    new Label
                    {
                        Text = string.IsNullOrWhiteSpace(book.Author) ? Loc.GetString("library-ui-unknown") : book.Author,
                        HorizontalExpand = true,
                        ClipText = true,
                        SizeFlagsStretchRatio = 2,
                        FontColorOverride = Color.Gray,
                    },
                    track,
                },
            });
        }

        if (matches == 0)
            _list.AddChild(new Label { Text = Loc.GetString("library-pinpointer-ui-no-match") });
        else if (matches > shown)
            _more.Text = Loc.GetString("library-pinpointer-ui-more", ("shown", shown), ("total", matches));
    }
}

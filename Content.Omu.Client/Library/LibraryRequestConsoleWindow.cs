// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Omu.Shared.Library;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Omu.Client.Library;

/// <summary>
///     The book request console: two tabs, one to ask for a book of the archive and one to ask for a blank book
///     with a title and a cover. The requests of this console and their answers are listed below the tabs.
/// </summary>
public sealed class LibraryRequestConsoleWindow : DefaultWindow
{
    public event Action<string, string, int>? OnSearch;
    public event Action<int>? OnRequestArchive;
    public event Action<string, LibraryCover>? OnRequestBlank;

    private readonly Label _status = new() { ClipText = true };
    private readonly TabContainer _tabs = new() { VerticalExpand = true, HorizontalExpand = true };

    // Archive book request tab
    private readonly LineEdit _titleEdit = new() { PlaceHolder = Loc.GetString("library-ui-title"), HorizontalExpand = true };
    private readonly LineEdit _authorEdit = new() { PlaceHolder = Loc.GetString("library-ui-author"), HorizontalExpand = true };
    private readonly BoxContainer _results = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Button _prevPage = new() { Text = "<" };
    private readonly Button _nextPage = new() { Text = ">" };
    private readonly Label _pageLabel = new() { MinWidth = 90, Align = Label.AlignMode.Center };

    // Blank book request tab
    private readonly LineEdit _blankTitle = new() { PlaceHolder = Loc.GetString("library-ui-blank-title"), HorizontalExpand = true };
    private readonly LibraryCoverPicker _blankCover = new();

    private int _page;
    private bool _linked;

    public LibraryRequestConsoleWindow()
    {
        Title = Loc.GetString("library-request-ui-title");
        MinSize = new Vector2(520, 560);
        SetSize = new Vector2(600, 660);

        var archive = BuildArchiveTab();
        var blank = BuildBlankTab();
        _tabs.AddChild(archive);
        _tabs.AddChild(blank);
        TabContainer.SetTabTitle(archive, Loc.GetString("library-request-ui-tab-archive"));
        TabContainer.SetTabTitle(blank, Loc.GetString("library-request-ui-tab-blank"));

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true,
            Margin = new Thickness(4),
            Children =
            {
                _status,
                _tabs,
            },
        });

        _titleEdit.OnTextEntered += _ => Search(0);
        _authorEdit.OnTextEntered += _ => Search(0);
        _prevPage.OnPressed += _ => Search(_page - 1);
        _nextPage.OnPressed += _ => Search(_page + 1);
    }

    private Control BuildArchiveTab()
    {
        var search = new Button { Text = Loc.GetString("library-ui-search") };
        search.OnPressed += _ => Search(0);

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { _titleEdit, _authorEdit, search },
                },
                new ScrollContainer
                {
                    VerticalExpand = true,
                    HorizontalExpand = true,
                    HScrollEnabled = false,
                    Children = { _results },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    HorizontalAlignment = Control.HAlignment.Center,
                    Children = { _prevPage, _pageLabel, _nextPage },
                },
            },
        };
    }

    private Control BuildBlankTab()
    {
        var send = new Button { Text = Loc.GetString("library-request-ui-send") };
        send.OnPressed += _ => OnRequestBlank?.Invoke(_blankTitle.Text, _blankCover.Cover);

        var info = new Label { Text = Loc.GetString("library-request-ui-blank-info"), FontColorOverride = Color.Gray };

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            Children =
            {
                info,
                _blankTitle,
                new Label { Text = Loc.GetString("library-ui-cover-title") },
                _blankCover,
                send,
            },
        };
    }

    private void Search(int page)
    {
        OnSearch?.Invoke(_titleEdit.Text, _authorEdit.Text, Math.Max(page, 0));
    }

    public void UpdateState(LibraryRequestConsoleBoundUserInterfaceState state)
    {
        _linked = state.Linked;
        _status.Text = state.Linked
            ? Loc.GetString("library-request-ui-linked")
            : Loc.GetString("library-request-ui-not-linked");
        _status.FontColorOverride = state.Linked ? Color.LightGreen : Color.Orange;

        _page = state.Page;
        _pageLabel.Text = $"{state.Page + 1} / {state.PageCount + 1}";
        _prevPage.Disabled = state.Page <= 0;
        _nextPage.Disabled = state.Page >= state.PageCount;

        UpdateResults(state);
    }

    private void UpdateResults(LibraryRequestConsoleBoundUserInterfaceState state)
    {
        _results.RemoveAllChildren();

        if (!state.ArchiveAvailable)
        {
            _results.AddChild(new Label { Text = Loc.GetString("library-ui-archive-offline") });
            return;
        }

        if (state.Results.Count == 0)
        {
            _results.AddChild(new Label { Text = Loc.GetString("library-ui-no-results") });
            return;
        }

        foreach (var book in state.Results)
        {
            var request = new Button
            {
                Text = Loc.GetString("library-request-ui-request"),
                Disabled = !_linked,
            };
            var id = book.Id;
            request.OnPressed += _ => OnRequestArchive?.Invoke(id);

            _results.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children =
                {
                    new Label { Text = $"#{book.Id}", MinWidth = 50 },
                    new Label { Text = book.Title, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 3 },
                    new Label
                    {
                        Text = string.IsNullOrWhiteSpace(book.Author) ? Loc.GetString("library-ui-unknown") : book.Author,
                        HorizontalExpand = true,
                        ClipText = true,
                        SizeFlagsStretchRatio = 2,
                    },
                    new Label { Text = book.Category, MinWidth = 90 },
                    request,
                },
            });
        }
    }
}

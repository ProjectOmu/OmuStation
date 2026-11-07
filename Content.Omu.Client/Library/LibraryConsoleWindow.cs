// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Message;
using Content.Client.UserInterface.Controls;
using Content.Omu.Shared.Library;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Omu.Client.Library;

/// <summary>
///     The library console window. Ported from tgstation's LibraryConsole tgui interface, with the same screens:
///     archive, inventory, checkouts and upload.
/// </summary>
public sealed class LibraryConsoleWindow : DefaultWindow
{
    public event Action<string, string, string, int?, int>? OnSearch;
    public event Action? OnClearSearch;
    public event Action<int>? OnPrint;
    public event Action<LibraryCover, string>? OnPrintBlank;
    public event Action<int>? OnRequestAccept;
    public event Action<int>? OnRequestDeny;
    public event Action? OnRefreshInventory;
    public event Action<int, int>? OnCheckout;
    public event Action? OnRegisterBook;
    public event Action<int>? OnClearMissing;
    public event Action? OnEjectId;
    public event Action<int>? OnCheckin;
    public event Action<int>? OnRemoveBook;
    public event Action<string, string>? OnSetCache;
    public event Action<string, LibraryCover>? OnUpload;
    public event Action<int, bool>? OnReview;
    public event Action<int>? OnReviewView;
    public event Action<int>? OnAdminDelete;
    public event Action<string, int>? OnAdminArchive;

    private readonly Label _status = new() { HorizontalExpand = true, ClipText = true };
    private readonly Label _binderStatus = new() { HorizontalExpand = true, ClipText = true };

    // Archive tab
    private readonly LineEdit _titleEdit = new() { PlaceHolder = Loc.GetString("library-ui-title"), HorizontalExpand = true };
    private readonly LineEdit _authorEdit = new() { PlaceHolder = Loc.GetString("library-ui-author"), HorizontalExpand = true };
    private readonly LineEdit _idEdit = new() { PlaceHolder = Loc.GetString("library-ui-id"), MinWidth = 70 };
    private readonly OptionButton _categorySearch = new();
    private readonly BoxContainer _results = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _pageLabel = new();
    private readonly Button _prevPage = new() { Text = "<" };
    private readonly Button _nextPage = new() { Text = ">" };

    // Inventory tab
    private readonly Label _idLabel = new() { HorizontalExpand = true, ClipText = true };
    private readonly Button _ejectId = new() { Text = Loc.GetString("library-ui-eject-id") };
    private readonly LineEdit _minutesEdit = new() { Text = LibraryConstants.DefaultLoanMinutes.ToString(), MinWidth = 60 };
    private readonly BoxContainer _inventory = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly LineEdit _inventorySearch = new() { PlaceHolder = Loc.GetString("library-ui-inventory-search"), HorizontalExpand = true };
    private LibraryConsoleBoundUserInterfaceState? _inventoryState;
    private readonly Button _registerBook = new() { Text = Loc.GetString("library-ui-register-book"), Disabled = true };

    // Checkouts tab
    private readonly BoxContainer _loans = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };

    // Upload tab
    private readonly Label _scannerLabel = new() { ClipText = true };
    private readonly LineEdit _cacheTitle = new() { PlaceHolder = Loc.GetString("library-ui-title"), HorizontalExpand = true };
    private readonly LineEdit _cacheAuthor = new() { PlaceHolder = Loc.GetString("library-ui-author"), HorizontalExpand = true };
    private readonly Label _cacheInfo = new() { ClipText = true };
    private readonly OptionButton _categoryUpload = new();
    private readonly Button _printBlankButton = new() { Text = Loc.GetString("library-ui-print-blank") };
    private readonly Button _uploadButton = new() { Text = Loc.GetString("library-ui-upload") };
    private readonly RichTextLabel _uploadWarning = new() { HorizontalExpand = true };
    private readonly RichTextLabel _reviewNotice = new() { HorizontalExpand = true };

    // Admin tab, only added when the server sends the review queue (it only does so for admins).
    private readonly TabContainer _tabs = new() { VerticalExpand = true, HorizontalExpand = true };
    private readonly BoxContainer _pending = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private Control? _reviewTab;
    private readonly OptionButton _adminMode = new();
    private readonly LineEdit _adminFilter = new() { PlaceHolder = "Title, author, user or id", HorizontalExpand = true };
    private readonly Button _adminSearch = new() { Text = "Search" };
    private readonly Button _adminPrev = new() { Text = "<" };
    private readonly Button _adminNext = new() { Text = ">" };
    private readonly Label _adminPageLabel = new();
    private readonly BoxContainer _adminArchiveBar = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, Visible = false };
    public string AdminFilter => _adminFilter.Text;
    private LibraryModerationMessage? _lastPending;
    private LibraryAdminArchiveMessage? _lastArchive;
    private bool _archiveMode;

    private readonly LibraryCoverPicker _uploadCover = new();
    private readonly LibraryCoverPicker _blankCover = new();
    private readonly LineEdit _blankTitle = new() { PlaceHolder = Loc.GetString("library-ui-blank-title"), HorizontalExpand = true };
    private readonly BoxContainer _requests = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private bool _hasId;

    private int _page;
    private int _pageCount;
    private bool _printReady = true;
    private bool _cacheEditsDirty;
    private bool _hadCache;

    public LibraryConsoleWindow()
    {

        Title = Loc.GetString("library-console-ui-title");
        MinSize = new Vector2(560, 560);
        SetSize = new Vector2(640, 680);

        foreach (var category in LibraryConstants.SearchCategories)
        {
            _categorySearch.AddItem(CategoryName(category));
        }

        foreach (var category in LibraryConstants.UploadCategories)
        {
            _categoryUpload.AddItem(CategoryName(category));
        }

        _categorySearch.OnItemSelected += args => args.Button.SelectId(args.Id);
        _categoryUpload.OnItemSelected += args => args.Button.SelectId(args.Id);

        _uploadWarning.SetMarkup($"[color=red][bold]{Loc.GetString("library-ui-upload-warning")}[/bold][/color]");
        _ejectId.OnPressed += _ => OnEjectId?.Invoke();

        _reviewNotice.SetMarkup($"[color=gray]{Loc.GetString("library-ui-review-notice")}[/color]");
        var tabs = _tabs;
        var archive = BuildArchiveTab();
        var inventory = BuildInventoryTab();
        var loans = BuildLoansTab();
        var upload = BuildUploadTab();
        var blank = BuildBlankTab();
        var requests = BuildRequestsTab();
        tabs.AddChild(archive);
        tabs.AddChild(inventory);
        tabs.AddChild(loans);
        tabs.AddChild(upload);
        tabs.AddChild(blank);
        tabs.AddChild(requests);
        TabContainer.SetTabTitle(archive, Loc.GetString("library-ui-tab-archive"));
        TabContainer.SetTabTitle(inventory, Loc.GetString("library-ui-tab-inventory"));
        TabContainer.SetTabTitle(loans, Loc.GetString("library-ui-tab-checkouts"));
        TabContainer.SetTabTitle(upload, Loc.GetString("library-ui-tab-upload"));
        TabContainer.SetTabTitle(blank, Loc.GetString("library-ui-tab-blank"));
        TabContainer.SetTabTitle(requests, Loc.GetString("library-ui-tab-requests"));

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true,
            Children = { _status, tabs },
        });

        _titleEdit.OnTextEntered += _ => Search(0);
        _authorEdit.OnTextEntered += _ => Search(0);
        _idEdit.OnTextEntered += _ => Search(0);
        _prevPage.OnPressed += _ => Search(_page - 1);
        _nextPage.OnPressed += _ => Search(_page + 1);

        _cacheTitle.OnTextChanged += _ => _cacheEditsDirty = true;
        _cacheAuthor.OnTextChanged += _ => _cacheEditsDirty = true;
        _cacheTitle.OnTextEntered += _ => PushCacheEdits();
        _cacheAuthor.OnTextEntered += _ => PushCacheEdits();
    }

    private static string CategoryName(string category)
    {
        return category == LibraryConstants.AnyCategory ? Loc.GetString("library-ui-category-any") : category;
    }

    private Control BuildArchiveTab()
    {
        var search = new Button { Text = Loc.GetString("library-ui-search") };
        search.OnPressed += _ => Search(0);

        var clear = new Button { Text = Loc.GetString("library-ui-clear") };
        clear.OnPressed += _ =>
        {
            _titleEdit.Text = string.Empty;
            _authorEdit.Text = string.Empty;
            _idEdit.Text = string.Empty;
            _categorySearch.SelectId(0);
            OnClearSearch?.Invoke();
        };

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            Children =
            {
                _binderStatus,
                _reviewNotice,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { _titleEdit, _authorEdit, _idEdit },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { _categorySearch, search, clear },
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

    private Control BuildInventoryTab()
    {
        var refresh = new Button { Text = Loc.GetString("library-ui-refresh") };
        refresh.OnPressed += _ => OnRefreshInventory?.Invoke();
        _registerBook.OnPressed += _ => OnRegisterBook?.Invoke();

        // The search only filters what the server already sent, there is nothing to ask the server for.
        _inventorySearch.OnTextChanged += _ =>
        {
            if (_inventoryState != null)
                UpdateInventory(_inventoryState);
        };

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children =
                    {
                        _idLabel,
                        _ejectId,
                        new Label { Text = Loc.GetString("library-ui-minutes") },
                        _minutesEdit,
                        refresh,
                        _registerBook,
                    },
                },
                _inventorySearch,
                new ScrollContainer
                {
                    VerticalExpand = true,
                    HorizontalExpand = true,
                    HScrollEnabled = false,
                    Children = { _inventory },
                },
            },
        };
    }

    private Control BuildLoansTab()
    {
        return new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
            Margin = new Thickness(4),
            Children = { _loans },
        };
    }

    private Control BuildRequestsTab()
    {
        return new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
            Margin = new Thickness(4),
            Children = { _requests },
        };
    }

    private void UpdateRequests(LibraryConsoleBoundUserInterfaceState state)
    {
        _requests.RemoveAllChildren();

        if (state.Requests.Count == 0)
        {
            _requests.AddChild(new Label { Text = Loc.GetString("library-ui-no-requests") });
            return;
        }

        foreach (var request in state.Requests)
        {
            var blank = request.Kind == LibraryRequestKind.Blank;
            var name = blank
                ? Loc.GetString("library-ui-request-blank", ("title", request.Title))
                : Loc.GetString("library-ui-request-archive", ("id", request.ArchiveId), ("title", request.Title), ("author", DisplayAuthor(request.Author)));

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children =
                {
                    new Label { Text = name, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 3 },
                    new Label { Text = request.Requester, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 1, FontColorOverride = Color.Gray },
                },
            };

            if (request.Status == LibraryRequestStatus.Pending)
            {
                var id = request.Id;

                // Blank book requests can be copied into the blank book tab, archive books are printed right here.
                if (blank)
                {
                    var fill = new Button { Text = Loc.GetString("library-ui-request-fill") };
                    var title = request.Title;
                    var cover = request.Cover;
                    fill.OnPressed += _ =>
                    {
                        _blankTitle.Text = title;
                        _blankCover.SetCover(cover);
                    };
                    row.AddChild(fill);
                }
                else
                {
                    var print = new Button { Text = Loc.GetString("library-ui-print"), Disabled = !_printReady };
                    var archiveId = request.ArchiveId;
                    print.OnPressed += _ => OnPrint?.Invoke(archiveId);
                    row.AddChild(print);
                }

                var accept = new Button { Text = Loc.GetString("library-ui-request-accept") };
                accept.OnPressed += _ => OnRequestAccept?.Invoke(id);
                var deny = new Button { Text = Loc.GetString("library-ui-request-deny") };
                deny.OnPressed += _ => OnRequestDeny?.Invoke(id);
                row.AddChild(accept);
                row.AddChild(deny);
            }
            else
            {
                row.AddChild(new Label
                {
                    Text = request.Status == LibraryRequestStatus.Accepted
                        ? Loc.GetString("library-ui-request-accepted")
                        : Loc.GetString("library-ui-request-denied"),
                    FontColorOverride = request.Status == LibraryRequestStatus.Accepted ? Color.LightGreen : Color.Red,
                    MinWidth = 90,
                });
            }

            _requests.AddChild(row);
        }
    }

    private Control BuildBlankTab()
    {
        _printBlankButton.OnPressed += _ => OnPrintBlank?.Invoke(_blankCover.Cover, _blankTitle.Text);

        var info = new RichTextLabel();
        info.SetMarkup($"[color=gray]{Loc.GetString("library-ui-blank-info")}[/color]");

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
                _printBlankButton,
            },
        };
    }

    private Control BuildUploadTab()
    {
        _uploadButton.OnPressed += _ =>
            OnUpload?.Invoke(LibraryConstants.UploadCategories[_categoryUpload.SelectedId], _uploadCover.Cover);

        var apply = new Button { Text = Loc.GetString("library-ui-apply") };
        apply.OnPressed += _ => PushCacheEdits();

        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            Children =
            {
                _scannerLabel,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { _cacheTitle, _cacheAuthor, apply },
                },
                _cacheInfo,
                new Label { Text = Loc.GetString("library-ui-cover-title") },
                _uploadCover,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { _categoryUpload, _uploadButton },
                },
                _uploadWarning,
            },
        };
    }

    private void Search(int page)
    {
        int? id = null;
        if (int.TryParse(_idEdit.Text, out var parsed) && parsed > 0)
            id = parsed;

        var category = LibraryConstants.SearchCategories[_categorySearch.SelectedId];
        OnSearch?.Invoke(_titleEdit.Text, _authorEdit.Text, category, id, Math.Max(page, 0));
    }

    private void PushCacheEdits()
    {
        _cacheEditsDirty = false;
        OnSetCache?.Invoke(_cacheTitle.Text, _cacheAuthor.Text);
    }

    public void UpdateState(LibraryConsoleBoundUserInterfaceState state)
    {
        _status.Text = state.ArchiveAvailable
            ? Loc.GetString("library-ui-status-online")
            : Loc.GetString("library-ui-status-offline");
        _status.FontColorOverride = state.ArchiveAvailable ? Color.LightGreen : Color.Red;

        _page = state.Page;
        _pageCount = state.PageCount;
        _printReady = state.CanPrint;
        _printBlankButton.Disabled = !state.CanPrint;

        _binderStatus.Text = state.HasBookbinder
            ? Loc.GetString("library-ui-binder-status", ("sheets", state.BinderSheets), ("cost", state.SheetsPerBook))
            : Loc.GetString("library-ui-no-binder");
        _binderStatus.FontColorOverride = !state.HasBookbinder || state.BinderSheets < state.SheetsPerBook
            ? Color.Orange
            : null;

        // Only mirror the server's search values onto the inputs the player is not typing in.
        if (!_titleEdit.HasKeyboardFocus())
            _titleEdit.Text = state.SearchTitle;
        if (!_authorEdit.HasKeyboardFocus())
            _authorEdit.Text = state.SearchAuthor;
        if (!_idEdit.HasKeyboardFocus())
            _idEdit.Text = state.SearchId?.ToString() ?? string.Empty;

        var categoryIndex = Array.IndexOf(LibraryConstants.SearchCategories, state.SearchCategory);
        _categorySearch.SelectId(categoryIndex < 0 ? 0 : categoryIndex);

        _pageLabel.Text = Loc.GetString("library-ui-page", ("page", state.Page + 1), ("pages", state.PageCount + 1));
        _prevPage.Disabled = state.Page <= 0;
        _nextPage.Disabled = state.Page >= state.PageCount;

        UpdateResults(state);
        UpdateInventory(state);
        UpdateLoans(state);
        UpdateRequests(state);
        UpdateUpload(state);
    }

    private void UpdateResults(LibraryConsoleBoundUserInterfaceState state)
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
            var print = new Button
            {
                Text = Loc.GetString("library-ui-print"),
                Disabled = !_printReady,
            };
            var id = book.Id;
            print.OnPressed += _ => OnPrint?.Invoke(id);

            _results.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children =
                {
                    new Label { Text = $"#{book.Id}", MinWidth = 50 },
                    new Label { Text = book.Title, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 3 },
                    new Label { Text = book.Author, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 2 },
                    new Label { Text = book.Category, MinWidth = 90 },
                    print,
                },
            });
        }
    }

    private void UpdateInventory(LibraryConsoleBoundUserInterfaceState state)
    {
        _inventoryState = state;
        _inventory.RemoveAllChildren();

        _hasId = state.HasId && !string.IsNullOrEmpty(state.IdName);
        _ejectId.Disabled = !state.HasId;
        _idLabel.Text = !state.HasId
            ? Loc.GetString("library-ui-no-id")
            : _hasId
                ? Loc.GetString("library-ui-id-inserted", ("name", state.IdName))
                : Loc.GetString("library-ui-id-no-name");
        _idLabel.FontColorOverride = _hasId ? null : Color.Orange;

        // Books are added by scanning them, the scanner has to be linked and hold one.
        _registerBook.Disabled = !(state.HasScanner && state.HasBook);

        if (state.Inventory.Count == 0)
        {
            _inventory.AddChild(new Label { Text = Loc.GetString("library-ui-no-inventory") });
            return;
        }

        var filter = _inventorySearch.Text.Trim();
        var shown = 0;

        foreach (var book in state.Inventory)
        {
            if (filter.Length > 0 &&
                !book.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !book.Author.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            shown++;
            var checkout = new Button { Text = Loc.GetString("library-ui-checkout"), Disabled = !_hasId || !book.InRoom || book.Status == LibraryBookStatus.CheckedOut };
            var bookId = book.Id;
            checkout.OnPressed += _ =>
            {
                var minutes = int.TryParse(_minutesEdit.Text, out var parsed)
                    ? parsed
                    : LibraryConstants.DefaultLoanMinutes;
                OnCheckout?.Invoke(bookId, minutes);
            };

            var remove = LibraryReviewWindow.MakeConfirm("library-ui-remove-book");
            remove.OnPressed += _ => OnRemoveBook?.Invoke(bookId);

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children =
                {
                    new Label { Text = book.Title, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 3 },
                    new Label { Text = DisplayAuthor(book.Author), HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 2 },
                    MakeStatusLabel(book),
                },
            };

            // The missing mark stays until a librarian clears it.
            if (book.Status == LibraryBookStatus.Missing)
            {
                var clear = new Button { Text = Loc.GetString("library-ui-clear-missing") };
                clear.OnPressed += _ => OnClearMissing?.Invoke(bookId);
                row.AddChild(clear);
            }

            row.AddChild(checkout);
            row.AddChild(remove);
            _inventory.AddChild(row);
        }

        if (shown == 0)
            _inventory.AddChild(new Label { Text = Loc.GetString("library-ui-no-inventory-match") });
    }

    private static Label MakeStatusLabel(LibraryInventoryEntry book)
    {
        var label = new Label { MinWidth = 120, ClipText = true };
        switch (book.Status)
        {
            case LibraryBookStatus.Missing:
                label.Text = Loc.GetString("library-ui-status-missing");
                label.FontColorOverride = Color.Red;
                break;
            case LibraryBookStatus.Taken:
                label.Text = Loc.GetString("library-ui-status-taken", ("seconds", book.SecondsLeft));
                label.FontColorOverride = Color.Orange;
                break;
            case LibraryBookStatus.CheckedOut:
                label.Text = Loc.GetString("library-ui-status-checked-out");
                label.FontColorOverride = Color.Gray;
                break;
            default:
                label.Text = Loc.GetString("library-ui-status-present");
                label.FontColorOverride = Color.LightGreen;
                break;
        }

        return label;
    }

    private void UpdateLoans(LibraryConsoleBoundUserInterfaceState state)
    {
        _loans.RemoveAllChildren();

        if (state.Loans.Count == 0)
        {
            _loans.AddChild(new Label { Text = Loc.GetString("library-ui-no-loans") });
            return;
        }

        foreach (var loan in state.Loans)
        {
            var checkin = new Button { Text = Loc.GetString("library-ui-checkin") };
            var loanId = loan.Id;
            checkin.OnPressed += _ => OnCheckin?.Invoke(loanId);

            var due = new Label
            {
                Text = loan.Indefinite
                    ? Loc.GetString("library-ui-indefinite")
                    : loan.Overdue
                    ? Loc.GetString("library-ui-overdue")
                    : Loc.GetString("library-ui-due-in", ("minutes", loan.DueInMinutes)),
                FontColorOverride = !loan.Indefinite && loan.Overdue ? Color.Red : null,
                MinWidth = 110,
            };

            _loans.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children =
                {
                    new Label { Text = loan.Title, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 3 },
                    new Label { Text = loan.Borrower, HorizontalExpand = true, ClipText = true, SizeFlagsStretchRatio = 2 },
                    due,
                    checkin,
                },
            });
        }
    }

    private void UpdateUpload(LibraryConsoleBoundUserInterfaceState state)
    {
        if (!state.HasScanner)
            _scannerLabel.Text = Loc.GetString("library-ui-no-scanner");
        else if (state.HasBook)
            _scannerLabel.Text = Loc.GetString("library-ui-scanner-has-book");
        else
            _scannerLabel.Text = Loc.GetString("library-ui-scanner-empty");

        var editable = state.HasScanner && state.HasCache;
        _cacheTitle.Editable = editable;
        _cacheAuthor.Editable = editable;

        // Don't overwrite what the player is typing, but do load a freshly scanned book.
        var freshCache = state.HasCache && !_hadCache;
        _hadCache = state.HasCache;

        if (!state.HasCache)
        {
            _cacheTitle.Text = string.Empty;
            _cacheAuthor.Text = string.Empty;
            _cacheEditsDirty = false;
        }
        else if (freshCache || !_cacheEditsDirty)
        {
            _cacheTitle.Text = state.CacheTitle;
            _cacheAuthor.Text = state.CacheAuthor;
            _cacheEditsDirty = false;
        }

        _cacheInfo.Text = state.HasCache
            ? Loc.GetString("library-ui-cache-info", ("length", state.CacheLength))
            : Loc.GetString("library-ui-no-cache");

        _uploadButton.Disabled = !(state.ArchiveAvailable && state.UploadsEnabled && state.HasScanner && state.HasCache);
        _categoryUpload.Disabled = _uploadButton.Disabled;
    }

    /// <summary>
    ///     Builds the admin tab the first time the server sends the review queue. Only admins get it.
    /// </summary>
    private void EnsureAdminTab()
    {
        if (_reviewTab != null)
            return;

        _adminMode.AddItem(Loc.GetString("library-ui-admin-mode-pending"), 0);
        _adminMode.AddItem(Loc.GetString("library-ui-admin-mode-archive"), 1);
        _adminMode.OnItemSelected += args =>
        {
            _adminMode.SelectId(args.Id);
            _archiveMode = args.Id == 1;
            _adminArchiveBar.Visible = _archiveMode;
            if (_archiveMode)
                OnAdminArchive?.Invoke(_adminFilter.Text, 0);

            RefreshAdminList();
        };

        _adminSearch.OnPressed += _ => OnAdminArchive?.Invoke(_adminFilter.Text, 0);
        _adminFilter.OnTextEntered += _ => OnAdminArchive?.Invoke(_adminFilter.Text, 0);
        _adminPrev.OnPressed += _ => OnAdminArchive?.Invoke(_adminFilter.Text, (_lastArchive?.Page ?? 0) - 1);
        _adminNext.OnPressed += _ => OnAdminArchive?.Invoke(_adminFilter.Text, (_lastArchive?.Page ?? 0) + 1);

        _adminArchiveBar.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children = { _adminFilter, _adminSearch },
        });

        _reviewTab = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            Children =
            {
                _adminMode,
                _adminArchiveBar,
                new ScrollContainer
                {
                    VerticalExpand = true,
                    HorizontalExpand = true,
                    HScrollEnabled = false,
                    Children = { _pending },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    HorizontalAlignment = Control.HAlignment.Center,
                    Children = { _adminPrev, _adminPageLabel, _adminNext },
                },
            },
        };
        _tabs.AddChild(_reviewTab);
        TabContainer.SetTabTitle(_reviewTab, Loc.GetString("library-ui-tab-review"));
    }

    public void UpdateModeration(LibraryModerationMessage message)
    {
        EnsureAdminTab();
        _lastPending = message;
        RefreshAdminList();
    }

    public void UpdateAdminArchive(LibraryAdminArchiveMessage message)
    {
        EnsureAdminTab();
        _lastArchive = message;
        RefreshAdminList();
    }

    private void RefreshAdminList()
    {
        _pending.RemoveAllChildren();

        var showArchive = _archiveMode;
        _adminPrev.Visible = showArchive;
        _adminNext.Visible = showArchive;
        _adminPageLabel.Visible = showArchive;

        var books = showArchive ? _lastArchive?.Books : _lastPending?.Pending;
        if (showArchive && _lastArchive != null)
        {
            _adminPageLabel.Text = Loc.GetString("library-ui-page", ("page", _lastArchive.Page + 1), ("pages", _lastArchive.PageCount + 1));
            _adminPrev.Disabled = _lastArchive.Page <= 0;
            _adminNext.Disabled = _lastArchive.Page >= _lastArchive.PageCount;
        }

        if (books == null || books.Count == 0)
        {
            _pending.AddChild(new Label
            {
                Text = Loc.GetString(showArchive ? "library-ui-admin-archive-empty" : "library-ui-review-empty"),
            });
            return;
        }

        foreach (var book in books)
        {
            var id = book.Id;
            var review = new Button { Text = Loc.GetString("library-ui-review-open") };
            review.OnPressed += _ => OnReviewView?.Invoke(id);

            var buttons = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children = { review },
            };

            if (showArchive)
            {
                var delete = LibraryReviewWindow.MakeConfirm("library-ui-admin-delete");
                delete.OnPressed += _ => OnAdminDelete?.Invoke(id);
                buttons.AddChild(delete);
            }
            else
            {
                var accept = LibraryReviewWindow.MakeConfirm("library-ui-review-accept");
                accept.OnPressed += _ => OnReview?.Invoke(id, true);

                var decline = LibraryReviewWindow.MakeConfirm("library-ui-review-decline");
                decline.OnPressed += _ => OnReview?.Invoke(id, false);

                buttons.AddChild(accept);
                buttons.AddChild(decline);
            }

            _pending.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(0, 0, 0, 6),
                Children =
                {
                    new Label
                    {
                        Text = Loc.GetString("library-ui-review-title",
                            ("id", book.Id),
                            ("title", book.Title),
                            ("author", DisplayAuthor(book.Author))),
                        HorizontalExpand = true,
                        ClipText = true,
                    },
                    new Label
                    {
                        Text = Loc.GetString("library-ui-review-uploader",
                            ("user", book.UploaderUser),
                            ("character", book.UploaderCharacter),
                            ("category", book.Category),
                            ("length", book.Length)),
                        FontColorOverride = Color.Gray,
                        HorizontalExpand = true,
                        ClipText = true,
                    },
                    buttons,
                },
            });
        }
    }

    private static string DisplayAuthor(string author)
    {
        return string.IsNullOrWhiteSpace(author) ? Loc.GetString("library-ui-unknown") : author;
    }
}

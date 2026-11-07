// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Library;

/// <summary>
///     The library scanner: put a book in, scan it, and the console can upload the digital copy to the archive.
///     Ported from tgstation's /obj/machinery/libraryscanner.
/// </summary>
public sealed class LibraryScannerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly LibraryAccessSystem _libraryAccess = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LibraryScannerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LibraryScannerComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<LibraryScannerComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<LibraryScannerComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<LibraryScannerComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<LibraryScannerComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<LibraryScannerComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<LibraryScannerComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
        SubscribeLocalEvent<LibraryScannerComponent, LibraryScannerScanMessage>(OnScan);
        SubscribeLocalEvent<LibraryScannerComponent, LibraryScannerClearMessage>(OnClear);
        SubscribeLocalEvent<LibraryScannerComponent, LibraryScannerEjectMessage>(OnEject);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LibraryScannerComponent>();
        while (query.MoveNext(out var uid, out var scanner))
        {
            if (scanner.ScanEnd is not { } end || _timing.CurTime < end)
                continue;

            scanner.ScanEnd = null;
            _appearance.SetData(uid, LibraryScannerVisuals.Scanning, false);
        }
    }

    private void OnInit(EntityUid uid, LibraryScannerComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, LibraryScannerComponent.BookSlotId, component.BookSlot);
    }

    private void OnRemove(EntityUid uid, LibraryScannerComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.BookSlot);
    }

    private void OnSlotChanged(EntityUid uid, LibraryScannerComponent component, ContainerModifiedMessage args)
    {
        if (!component.Initialized || args.Container.ID != component.BookSlot.ID)
            return;

        UpdateUi(uid, component);
        RefreshConsoles();
    }

    private void OnUiOpenAttempt(EntityUid uid, LibraryScannerComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!_libraryAccess.IsAllowed(uid, args.User, args.Silent))
            args.Cancel();
    }

    private void OnInsertAttempt(EntityUid uid, LibraryScannerComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.User is { } user && !_libraryAccess.IsAllowed(uid, user))
            args.Cancelled = true;
    }

    private void OnEjectAttempt(EntityUid uid, LibraryScannerComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.User is { } user && !_libraryAccess.IsAllowed(uid, user))
            args.Cancelled = true;
    }

    private void OnUiOpened(EntityUid uid, LibraryScannerComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnScan(EntityUid uid, LibraryScannerComponent component, LibraryScannerScanMessage args)
    {
        if (!_power.IsPowered(uid))
            return;

        if (component.BookSlot.Item is not { } book)
            return;

        if (!TryReadBook(book, out var data, out var failure))
        {
            _popup.PopupEntity(Loc.GetString(failure), uid);
            return;
        }

        if (component.Cache == data)
        {
            _popup.PopupEntity(Loc.GetString("library-scanner-already-cached"), uid);
            return;
        }

        component.Cache = data;
        component.ScanEnd = _timing.CurTime + component.ScanAnimationTime;
        _appearance.SetData(uid, LibraryScannerVisuals.Scanning, true);
        _audio.PlayPvs(component.ScanSound, uid);
        UpdateUi(uid, component);
        RefreshConsoles();
    }

    private void OnClear(EntityUid uid, LibraryScannerComponent component, LibraryScannerClearMessage args)
    {
        if (component.Cache == null)
            return;

        component.Cache = null;
        UpdateUi(uid, component);
        RefreshConsoles();
    }

    private void OnEject(EntityUid uid, LibraryScannerComponent component, LibraryScannerEjectMessage args)
    {
        if (component.BookSlot.Item == null)
            return;

        _itemSlots.TryEjectToHands(uid, component.BookSlot, args.Actor, true);
        _audio.PlayPvs(component.EjectSound, uid);
    }

    /// <summary>
    ///     Turns a physical book into its digital copy. Fails for entities that are not books, books that
    ///     are marked unique and blank books.
    /// </summary>
    public bool TryReadBook(EntityUid book, out LibraryBookData data, out string failure)
    {
        data = default!;
        failure = string.Empty;

        if (!TryComp<PaperComponent>(book, out var paper))
        {
            failure = "library-scanner-not-a-book";
            return false;
        }

        var author = string.Empty;
        if (TryComp<LibraryBookComponent>(book, out var info))
        {
            if (info.Unique)
            {
                failure = "library-scanner-unique";
                return false;
            }

            author = info.Author;
        }

        var content = LibraryText.CleanContent(paper.Content, LibraryConstants.MaxContentLength);
        if (string.IsNullOrWhiteSpace(content))
        {
            failure = "library-scanner-blank";
            return false;
        }

        var title = LibraryText.CleanLine(Name(book), LibraryConstants.MaxTitleLength);
        author = LibraryText.CleanLine(author, LibraryConstants.MaxAuthorLength);
        data = new LibraryBookData(title, author, content);
        return true;
    }

    /// <summary>
    ///     Replaces the title and author of the cached book. Does nothing when nothing is cached.
    /// </summary>
    public void SetCacheDetails(Entity<LibraryScannerComponent> scanner, string title, string author)
    {
        if (scanner.Comp.Cache is not { } cache)
            return;

        scanner.Comp.Cache = cache with
        {
            Title = LibraryText.CleanLine(title, LibraryConstants.MaxTitleLength),
            Author = LibraryText.CleanLine(author, LibraryConstants.MaxAuthorLength),
        };

        UpdateUi(scanner, scanner.Comp);
    }

    private void RefreshConsoles()
    {
        var consoles = EntityQueryEnumerator<LibraryConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            RaiseLocalEvent(uid, new LibraryConsoleRefreshEvent());
        }
    }

    public void UpdateUi(EntityUid uid, LibraryScannerComponent component)
    {
        var book = component.BookSlot.Item;
        var canScan = book != null && TryReadBook(book.Value, out _, out _);

        var state = new LibraryScannerBoundUserInterfaceState(
            book != null,
            canScan,
            component.Cache != null,
            component.Cache?.Title ?? string.Empty,
            component.Cache?.Author ?? string.Empty);

        _ui.SetUiState(uid, LibraryScannerUiKey.Key, state);
    }
}

/// <summary>
///     Raised on library consoles when something they display (scanner contents, archive) changed.
/// </summary>
public sealed class LibraryConsoleRefreshEvent : EntityEventArgs;

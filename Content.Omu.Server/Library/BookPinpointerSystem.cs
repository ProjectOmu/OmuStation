// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Omu.Shared.Library;
using Content.Server.Pinpointer;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Library;

/// <summary>
///     The book pinpointers. Using one opens a menu of books (the ones in the user's room, or every book on the
///     station for the universal one). A picked book becomes the target of the normal pinpointer, which draws the
///     arrow. Using the pinpointer again switches it off.
/// </summary>
public sealed class BookPinpointerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LibraryConsoleSystem _console = default!;
    [Dependency] private readonly LibraryRoomSystem _room = default!;
    [Dependency] private readonly PinpointerSystem _pinpointer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        // Before the normal pinpointer, which would just switch itself on and off.
        SubscribeLocalEvent<BookPinpointerComponent, ActivateInWorldEvent>(
            OnActivate,
            before: new[] { typeof(PinpointerSystem) });
        SubscribeLocalEvent<BookPinpointerComponent, BookPinpointerSelectMessage>(OnSelect);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BookPinpointerComponent, PinpointerComponent>();
        while (query.MoveNext(out var uid, out var book, out var pinpointer))
        {
            if (!pinpointer.IsActive || now < book.NextCheck)
                continue;

            book.NextCheck = now + CheckInterval;

            // The tracked book was destroyed, or it is too far away: the signal is lost.
            var target = pinpointer.Targets.FirstOrDefault();
            if (target == default || !Exists(target) || TerminatingOrDeleted(target) || IsTooFar(uid, book, target))
            {
                _popup.PopupEntity(Loc.GetString("library-pinpointer-lost"), uid);
                _pinpointer.TogglePinpointer(uid, pinpointer);
            }
        }
    }

    private bool IsTooFar(EntityUid uid, BookPinpointerComponent book, EntityUid target)
    {
        if (book.MaxRange <= 0f)
            return false;

        var from = Transform(uid);
        var to = Transform(target);
        if (from.MapID != to.MapID)
            return true;

        var distance = (_transform.GetWorldPosition(to) - _transform.GetWorldPosition(from)).Length();
        return distance > book.MaxRange;
    }

    private void OnActivate(EntityUid uid, BookPinpointerComponent book, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !TryComp<PinpointerComponent>(uid, out var pinpointer))
            return;

        args.Handled = true;

        // Using it while it tracks a book switches it off.
        if (pinpointer.IsActive)
        {
            _pinpointer.TogglePinpointer(uid, pinpointer);
            _popup.PopupEntity(Loc.GetString("library-pinpointer-off"), uid, args.User);
            return;
        }

        var books = FindBooks(book, args.User);
        var missing = FindMissing(book);
        var overdue = FindOverdue(book);
        if (books.Count == 0 && missing.Count == 0 && overdue.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString(book.Universal
                ? "library-pinpointer-no-books-station"
                : "library-pinpointer-no-books-room"), uid, args.User);
            return;
        }

        _ui.OpenUi(uid, BookPinpointerUiKey.Key, args.User);
        _ui.SetUiState(uid, BookPinpointerUiKey.Key, BuildState(book, books, missing, overdue));
    }

    private void OnSelect(EntityUid uid, BookPinpointerComponent book, BookPinpointerSelectMessage args)
    {
        if (!TryGetEntity(args.Book, out var target) || !TryComp<PinpointerComponent>(uid, out var pinpointer))
            return;

        // Only a book the menu lists can be picked, found again from where the user stands now.
        if (!FindBooks(book, args.Actor).Contains(target.Value) && !FindMissing(book).Contains(target.Value)
            && !FindOverdue(book).Any(o => o.Book == target.Value))
            return;

        _pinpointer.SetTarget(uid, target.Value, pinpointer);
        book.NextCheck = _timing.CurTime + CheckInterval;

        if (!pinpointer.IsActive)
            _pinpointer.TogglePinpointer(uid, pinpointer);

        _popup.PopupEntity(Loc.GetString("library-pinpointer-tracking", ("title", Name(target.Value))), uid, args.Actor);
        _ui.CloseUi(uid, BookPinpointerUiKey.Key, args.Actor);
    }

    /// <summary>
    ///     The books the menu offers, found from where <paramref name="user"/> stands.
    /// </summary>
    private List<EntityUid> FindBooks(BookPinpointerComponent book, EntityUid user)
    {
        return book.Universal ? _room.FindStationBooks(user) : _room.FindRoomBooks(user);
    }

    /// <summary>
    ///     The books library consoles marked as missing. Only the universal pinpointer lists them.
    /// </summary>
    private List<EntityUid> FindMissing(BookPinpointerComponent book)
    {
        return book.Universal ? _console.GetMissingBooks() : new List<EntityUid>();
    }

    private List<(EntityUid Book, string Borrower)> FindOverdue(BookPinpointerComponent book)
    {
        return book.Universal ? _console.GetOverdueBooks() : new List<(EntityUid, string)>();
    }

    private List<BookPinpointerEntry> MakeOverdueEntries(List<(EntityUid Book, string Borrower)> overdue)
    {
        return overdue
            .OrderBy(o => Name(o.Book), StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Book.Id)
            .Take(LibraryConstants.MaxPinpointerBooks)
            .Select(o => new BookPinpointerEntry(
                GetNetEntity(o.Book),
                LibraryText.CleanLine(Name(o.Book), LibraryConstants.MaxTitleLength),
                TryComp<LibraryBookComponent>(o.Book, out var info)
                    ? LibraryText.CleanLine(info.Author, LibraryConstants.MaxAuthorLength)
                    : string.Empty,
                o.Borrower))
            .ToList();
    }

    private List<BookPinpointerEntry> MakeEntries(List<EntityUid> books)
    {
        return books
            .OrderBy(b => Name(b), StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.Id)
            .Take(LibraryConstants.MaxPinpointerBooks)
            .Select(b => new BookPinpointerEntry(
                GetNetEntity(b),
                LibraryText.CleanLine(Name(b), LibraryConstants.MaxTitleLength),
                TryComp<LibraryBookComponent>(b, out var info)
                    ? LibraryText.CleanLine(info.Author, LibraryConstants.MaxAuthorLength)
                    : string.Empty))
            .ToList();
    }

    private BookPinpointerBoundUserInterfaceState BuildState(
        BookPinpointerComponent book,
        List<EntityUid> books,
        List<EntityUid> missing,
        List<(EntityUid Book, string Borrower)> overdue)
    {
        return new BookPinpointerBoundUserInterfaceState(
            MakeEntries(books),
            MakeEntries(missing),
            MakeOverdueEntries(overdue),
            book.Universal);
    }
}

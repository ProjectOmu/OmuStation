// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Library;

[Serializable, NetSerializable]
public enum LibraryConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum LibraryScannerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum LibraryScannerVisuals : byte
{
    Scanning,
}

public enum LibraryScannerVisualLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum LibraryRequestConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum LibraryRequestKind : byte
{
    /// <summary>A book of the archive.</summary>
    Archive,

    /// <summary>An empty book with a title and a cover.</summary>
    Blank,
}

[Serializable, NetSerializable]
public enum LibraryRequestStatus : byte
{
    Pending,
    Accepted,
    Denied,
}

/// <summary>
///     A book request as the library console and the request console show it.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct LibraryRequestEntry(
    int Id,
    LibraryRequestKind Kind,
    int ArchiveId,
    string Title,
    string Author,
    LibraryCover Cover,
    string Requester,
    LibraryRequestStatus Status);

/// <summary>
///     One row of an archive search result.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct LibraryBookSummary(int Id, string Title, string Author, string Category);

/// <summary>
///     A physical book found near a console, part of the "inventory" screen.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct LibraryInventoryEntry(
    int Id,
    string Title,
    string Author,
    LibraryBookStatus Status,
    int SecondsLeft,
    bool InRoom);

/// <summary>
///     Where a book of the inventory is.
/// </summary>
[Serializable, NetSerializable]
public enum LibraryBookStatus : byte
{
    /// <summary>In the library.</summary>
    Present,

    /// <summary>Taken out of the library, it becomes missing if it is not checked out in time.</summary>
    Taken,

    /// <summary>Taken and not checked out in time.</summary>
    Missing,

    /// <summary>Lent out, see the checkouts tab.</summary>
    CheckedOut,
}

/// <summary>
///     A book that is currently checked out to somebody.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct LibraryLoanEntry(
    int Id,
    string Title,
    string Author,
    string Borrower,
    float DueInMinutes,
    bool Overdue,
    bool Indefinite = false);

[Serializable, NetSerializable]
public sealed class LibraryConsoleBoundUserInterfaceState(
    bool archiveAvailable,
    bool uploadsEnabled,
    bool canPrint,
    string searchTitle,
    string searchAuthor,
    string searchCategory,
    int? searchId,
    int page,
    int pageCount,
    List<LibraryBookSummary> results,
    List<LibraryInventoryEntry> inventory,
    List<LibraryLoanEntry> loans,
    bool hasScanner,
    bool hasBook,
    bool hasCache,
    string cacheTitle,
    string cacheAuthor,
    int cacheLength,
    bool hasId,
    string idName,
    bool hasBookbinder,
    int binderSheets,
    int sheetsPerBook,
    List<LibraryRequestEntry> requests)
    : BoundUserInterfaceState
{
    /// <summary>Book requests from request consoles, the ones waiting first.</summary>
    public readonly List<LibraryRequestEntry> Requests = requests;

    /// <summary>False when the archive could not be reached (the tgstation "can_connect").</summary>
    public readonly bool ArchiveAvailable = archiveAvailable;

    public readonly bool UploadsEnabled = uploadsEnabled;

    /// <summary>False while the printer is busy, out of paper or missing.</summary>
    public readonly bool CanPrint = canPrint;

    public readonly string SearchTitle = searchTitle;
    public readonly string SearchAuthor = searchAuthor;
    public readonly string SearchCategory = searchCategory;
    public readonly int? SearchId = searchId;

    /// <summary>Zero based current page of the search.</summary>
    public readonly int Page = page;

    /// <summary>Zero based index of the last page of the search.</summary>
    public readonly int PageCount = pageCount;

    public readonly List<LibraryBookSummary> Results = results;
    public readonly List<LibraryInventoryEntry> Inventory = inventory;
    public readonly List<LibraryLoanEntry> Loans = loans;

    public readonly bool HasScanner = hasScanner;

    /// <summary>The scanner currently holds a book that could be scanned.</summary>
    public readonly bool HasBook = hasBook;

    public readonly bool HasCache = hasCache;
    public readonly string CacheTitle = cacheTitle;
    public readonly string CacheAuthor = cacheAuthor;
    public readonly int CacheLength = cacheLength;

    /// <summary>A borrower's ID is in the console.</summary>
    public readonly bool HasId = hasId;

    /// <summary>The name on the inserted ID, empty when it has none.</summary>
    public readonly string IdName = idName;

    /// <summary>A book binder is close enough to print books.</summary>
    public readonly bool HasBookbinder = hasBookbinder;

    public readonly int BinderSheets = binderSheets;
    public readonly int SheetsPerBook = sheetsPerBook;
}

[Serializable, NetSerializable]
public sealed class LibrarySearchMessage(string title, string author, string category, int? id, int page)
    : BoundUserInterfaceMessage
{
    public readonly string Title = title;
    public readonly string Author = author;
    public readonly string Category = category;
    public readonly int? Id = id;

    /// <summary>Zero based page to show.</summary>
    public readonly int Page = page;
}

[Serializable, NetSerializable]
public sealed class LibraryClearSearchMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class LibraryPrintBookMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class LibraryRefreshInventoryMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class LibraryCheckoutMessage(int id, int minutes) : BoundUserInterfaceMessage
{
    /// <summary>Id of the inventory entry.</summary>
    public readonly int Id = id;
    public readonly int Minutes = minutes;
}

/// <summary>
///     Removes a book's entry from the inventory. The book itself stays where it is.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryRemoveBookMessage(int id) : BoundUserInterfaceMessage
{
    /// <summary>Id of the inventory entry.</summary>
    public readonly int Id = id;
}

/// <summary>
///     A librarian clears the "missing" mark of a book by hand.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryClearMissingMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

/// <summary>
///     Adds the book that is in the scanner to the inventory. It has to be a book of the archive.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryRegisterBookMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class LibraryEjectIdMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class LibraryCheckinMessage(int loanId) : BoundUserInterfaceMessage
{
    public readonly int LoanId = loanId;
}

[Serializable, NetSerializable]
public sealed class LibrarySetCacheMessage(string title, string author) : BoundUserInterfaceMessage
{
    public readonly string Title = title;
    public readonly string Author = author;
}

/// <summary>
///     Prints an empty book with the chosen cover. It is not part of the archive until its text was scanned and
///     uploaded, and an admin approved it.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryPrintBlankMessage(LibraryCover cover, string title) : BoundUserInterfaceMessage
{
    public readonly LibraryCover Cover = cover;
    public readonly string Title = title;
}

[Serializable, NetSerializable]
public sealed class LibraryUploadMessage(string category, LibraryCover cover) : BoundUserInterfaceMessage
{
    public readonly string Category = category;
    public readonly LibraryCover Cover = cover;
}

[Serializable, NetSerializable]
public sealed class LibraryScannerBoundUserInterfaceState(
    bool hasBook,
    bool canScan,
    bool hasCache,
    string cacheTitle,
    string cacheAuthor)
    : BoundUserInterfaceState
{
    public readonly bool HasBook = hasBook;

    /// <summary>The held book can be scanned, i.e. it is not unique and has some text in it.</summary>
    public readonly bool CanScan = canScan;

    public readonly bool HasCache = hasCache;
    public readonly string CacheTitle = cacheTitle;
    public readonly string CacheAuthor = cacheAuthor;
}

[Serializable, NetSerializable]
public sealed class LibraryScannerScanMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class LibraryScannerClearMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class LibraryScannerEjectMessage : BoundUserInterfaceMessage;

/// <summary>
///     An uploaded book waiting for an admin to review it.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct LibraryPendingEntry(
    int Id,
    string Title,
    string Author,
    string Category,
    string UploaderUser,
    string UploaderCharacter,
    int Length);

/// <summary>
///     Sent by the server only to admins that have a console open: the books waiting for review.
///     Its arrival is what shows the admin tab.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryModerationMessage(List<LibraryPendingEntry> pending) : BoundUserInterfaceMessage
{
    public readonly List<LibraryPendingEntry> Pending = pending;
}

[Serializable, NetSerializable]
public sealed class LibraryReviewMessage(int id, bool accept) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly bool Accept = accept;
}

/// <summary>
///     Asks to read a book's text, used by the review window. Works for pending and archived books.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryReviewViewMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

/// <summary>
///     The full text of a book, sent only to the admin that asked for it.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryReviewTextMessage(LibraryPendingEntry book, string content, bool pending)
    : BoundUserInterfaceMessage
{
    public readonly LibraryPendingEntry Book = book;
    public readonly string Content = content;

    /// <summary>True if the book waits for review, false if it is already in the archive.</summary>
    public readonly bool Pending = pending;
}

[Serializable, NetSerializable]
public sealed class LibraryAdminArchiveRequestMessage(string filter, int page) : BoundUserInterfaceMessage
{
    public readonly string Filter = filter;
    public readonly int Page = page;
}

/// <summary>
///     One page of the archive for the admin tab, with the uploader of every book.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryAdminArchiveMessage(List<LibraryPendingEntry> books, string filter, int page, int pageCount)
    : BoundUserInterfaceMessage
{
    public readonly List<LibraryPendingEntry> Books = books;
    public readonly string Filter = filter;
    public readonly int Page = page;
    public readonly int PageCount = pageCount;
}

[Serializable, NetSerializable]
public sealed class LibraryAdminDeleteMessage(int id, string filter, int page) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly string Filter = filter;
    public readonly int Page = page;
}

[Serializable, NetSerializable]
public enum BookPinpointerUiKey : byte
{
    Key,
}

/// <summary>
///     A book in the menu of a book pinpointer.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct BookPinpointerEntry(NetEntity Book, string Title, string Author, string Borrower = "");

[Serializable, NetSerializable]
public sealed class BookPinpointerBoundUserInterfaceState(
    List<BookPinpointerEntry> books,
    List<BookPinpointerEntry> missing,
    List<BookPinpointerEntry> overdue,
    bool universal)
    : BoundUserInterfaceState
{
    public readonly List<BookPinpointerEntry> Books = books;

    /// <summary>Books a library console marked as missing. Only filled for the universal pinpointer.</summary>
    public readonly List<BookPinpointerEntry> Missing = missing;

    /// <summary>Books checked out past their return date, with the borrower. Only for the universal pinpointer.</summary>
    public readonly List<BookPinpointerEntry> Overdue = overdue;

    /// <summary>The librarian's universal pinpointer, it lists the whole station.</summary>
    public readonly bool Universal = universal;
}

[Serializable, NetSerializable]
public sealed class BookPinpointerSelectMessage(NetEntity book) : BoundUserInterfaceMessage
{
    public readonly NetEntity Book = book;
}

/// <summary>
///     The librarian accepts a book request. The requester sees it on their request console.
/// </summary>
[Serializable, NetSerializable]
public sealed class LibraryRequestAcceptMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class LibraryRequestDenyMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class LibraryRequestConsoleBoundUserInterfaceState(
    bool linked,
    bool archiveAvailable,
    string searchTitle,
    string searchAuthor,
    int page,
    int pageCount,
    List<LibraryBookSummary> results)
    : BoundUserInterfaceState
{
    /// <summary>A library console is linked to receive the requests.</summary>
    public readonly bool Linked = linked;

    public readonly bool ArchiveAvailable = archiveAvailable;
    public readonly string SearchTitle = searchTitle;
    public readonly string SearchAuthor = searchAuthor;
    public readonly int Page = page;
    public readonly int PageCount = pageCount;
    public readonly List<LibraryBookSummary> Results = results;
}

[Serializable, NetSerializable]
public sealed class LibraryRequestSearchMessage(string title, string author, int page) : BoundUserInterfaceMessage
{
    public readonly string Title = title;
    public readonly string Author = author;
    public readonly int Page = page;
}

[Serializable, NetSerializable]
public sealed class LibraryRequestArchiveMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class LibraryRequestBlankMessage(string title, LibraryCover cover) : BoundUserInterfaceMessage
{
    public readonly string Title = title;
    public readonly LibraryCover Cover = cover;
}


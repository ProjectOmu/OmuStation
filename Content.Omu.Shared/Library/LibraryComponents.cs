// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Library;

/// <summary>
///     The digital copy of a book that lives in a scanner's cache.
///     Ported from tgstation's /datum/book_info.
/// </summary>
public sealed record LibraryBookData(string Title, string Author, string Content);

/// <summary>
///     A loan on the console's checkout list. Purely bookkeeping, like in tgstation (/datum/borrowbook).
/// </summary>
public sealed class LibraryLoan
{
    public int Id;

    /// <summary>The book that was lent out. Loans follow the book's entity, never its title.</summary>
    public EntityUid Book;

    public string Title = string.Empty;
    public string Author = string.Empty;
    public string Borrower = string.Empty;
    public TimeSpan DueTime;

    /// <summary>Checked out without a return date. The book is still the library's property.</summary>
    public bool Indefinite;

    /// <summary>The ID card the book was checked out to, the borrower is messaged on its NanoChat.</summary>
    public EntityUid? BorrowerCard;

    /// <summary>The borrower was already told that the book is overdue.</summary>
    public bool OverdueNotified;
}

/// <summary>
///     The book scanner. Holds one book in an item slot and keeps a digital copy of the last scanned
///     book in its cache until a library console uploads it to the archive.
/// </summary>
[RegisterComponent]
public sealed partial class LibraryScannerComponent : Component
{
    public const string BookSlotId = "library_scanner_book";

    [DataField(required: true)]
    public ItemSlot BookSlot = new();

    [DataField]
    public SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/Machines/scanning.ogg");

    [DataField]
    public SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/Machines/tray_eject.ogg");

    /// <summary>
    ///     How long the scan line keeps sweeping across the scanner after a scan.
    /// </summary>
    [DataField]
    public TimeSpan ScanAnimationTime = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     When the scan animation ends, null while idle. Server only.
    /// </summary>
    public TimeSpan? ScanEnd;

    /// <summary>
    ///     The last scanned book. Server only, never networked.
    /// </summary>
    public LibraryBookData? Cache;
}

/// <summary>
///     The "book inventory management console": browse the archive, print books from it,
///     upload scanned books to it and keep track of checked out books.
/// </summary>
[RegisterComponent]
public sealed partial class LibraryConsoleComponent : Component
{
    public const string IdSlotId = "library_console_id";

    /// <summary>
    ///     The slot for the ID of the person borrowing a book. Check outs are made out to the name on this ID.
    /// </summary>
    [DataField(required: true)]
    public ItemSlot IdSlot = new();

    /// <summary>
    ///     Minimum time between two print requests (PRINTER_COOLDOWN in tgstation).
    /// </summary>
    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(6);

    /// <summary>
    ///     Minimum time between two archive requests (searches, page switches, uploads).
    /// </summary>
    [DataField]
    public TimeSpan RequestCooldown = TimeSpan.FromSeconds(0.5);

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    // Everything below is runtime state, server only.

    public string SearchTitle = string.Empty;
    public string SearchAuthor = string.Empty;
    public string SearchCategory = LibraryConstants.AnyCategory;
    public int? SearchId;
    public int SearchPage;

    public TimeSpan NextRequestTime;
    public TimeSpan NextPrintTime;

    /// <summary>
    ///     Set while the UI still has to be refreshed once the printer cooldown is over.
    /// </summary>
    public bool RefreshAfterCooldown;

    public readonly List<LibraryLoan> Loans = new();
    public int NextLoanId = 1;

    /// <summary>
    ///     Books people asked for from a book request console.
    /// </summary>
    public readonly List<LibraryBookRequest> Requests = new();

    public int NextRequestId = 1;

    /// <summary>
    ///     The library's inventory: the books that were registered with this console, by registration id.
    ///     A book that leaves the library stays here, it is marked missing after a while instead of vanishing.
    /// </summary>
    public readonly Dictionary<int, LibraryRegisteredBook> Registered = new();

    public int NextBookId = 1;

    /// <summary>
    ///     The books in the room when the round starts are registered once, shortly after the console appears.
    /// </summary>
    public bool InitialScanDone;

    public TimeSpan InitialScanTime;

    /// <summary>
    ///     When the registered books are checked against the room again.
    /// </summary>
    public TimeSpan NextEvaluate;
}

/// <summary>
///     A book in a console's inventory. Tracked by entity, so identical titles are never mixed up.
/// </summary>
public sealed class LibraryRegisteredBook
{
    public int Id;
    public EntityUid Book;
    public string Title = string.Empty;
    public string Author = string.Empty;

    /// <summary>In the console's room, on a shelf or carried by somebody.</summary>
    public bool InRoom;

    /// <summary>Since when the book is not in the library (not in the room, or carried). Null while it is.</summary>
    public TimeSpan? AbsentSince;

    /// <summary>Taken for longer than the delay and never checked out.</summary>
    public bool Missing;
}

/// <summary>
///     Binds a single sheet of paper into a book, crediting the author of the ID that was scanned.
///     Ported from tgstation's /obj/machinery/bookbinder.
/// </summary>
[RegisterComponent]
public sealed partial class BookbinderComponent : Component
{
    public const string PaperContainerId = "bookbinder_paper";

    [DataField]
    public TimeSpan BindTime = TimeSpan.FromSeconds(4.1);

    /// <summary>
    ///     How long printing a book from the archive takes.
    /// </summary>
    [DataField]
    public TimeSpan PrintTime = TimeSpan.FromSeconds(4.1);

    /// <summary>
    ///     Sheets of paper used, in addition to the written paper, to bind one book.
    /// </summary>
    [DataField]
    public int BindSheets = 4;

    /// <summary>
    ///     Sheets of paper used to print one book from the archive.
    /// </summary>
    [DataField]
    public int PrintSheets = 5;

    /// <summary>
    ///     Sheets of paper in the binder when it spawns. Machines that are built by hand use 0.
    /// </summary>
    [DataField]
    public int StartingSheets = 30;

    [DataField]
    public EntProtoId BookPrototype = "BookLibraryPrinted";

    [DataField]
    public SoundSpecifier BindSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier ScanIdSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    /// <summary>
    ///     The name that will be credited as the author of the next bound book.
    /// </summary>
    public string? ScannedAuthor;

    /// <summary>
    ///     When the current binding job finishes. Null while idle.
    /// </summary>
    public TimeSpan? BindingEnd;

    /// <summary>
    ///     When the current print job from the archive finishes. Null while idle.
    /// </summary>
    public TimeSpan? PrintEnd;

    public int PrintId;
    public EntityUid? PrintUser;

    /// <summary>
    ///     Set while the current print job is an empty book (with this cover) instead of an archive book.
    /// </summary>
    public LibraryCover? PrintBlankCover;

    /// <summary>The title that goes on the empty book of a blank print job.</summary>
    public string PrintBlankTitle = string.Empty;
}

/// <summary>
///     A pinpointer that is pointed at a book you pick from a menu. It works together with the normal
///     <c>Pinpointer</c> component, which draws the arrow and the distance. Using it opens the menu, using it again
///     while it tracks a book switches it off.
/// </summary>
[RegisterComponent]
public sealed partial class BookPinpointerComponent : Component
{
    /// <summary>
    ///     False: the menu lists the books of the room the user stands in. True (the librarian's universal
    ///     pinpointer): the menu lists every book on the station.
    /// </summary>
    [DataField]
    public bool Universal;

    /// <summary>
    ///     Tracking stops when the book is further away than this many tiles. 0 means no limit.
    /// </summary>
    [DataField]
    public float MaxRange = 30f;

    /// <summary>
    ///     When the range is checked again.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>
///     What somebody asked a library for through a book request console.
/// </summary>
public sealed class LibraryBookRequest
{
    public int Id;
    public LibraryRequestKind Kind;

    /// <summary>The archive book that was asked for, for archive requests.</summary>
    public int ArchiveId;

    /// <summary>The archive book's title, or the title the requester wants on their blank book.</summary>
    public string Title = string.Empty;

    public string Author = string.Empty;

    /// <summary>The cover the requester picked, for blank book requests.</summary>
    public LibraryCover Cover;

    public string Requester = string.Empty;

    /// <summary>The requester's ID card, the answer is sent to its NanoChat.</summary>
    public EntityUid? RequesterCard;

    /// <summary>The request console it came from.</summary>
    public EntityUid Source;

    public LibraryRequestStatus Status;
}

/// <summary>
///     A console where people ask the librarian for a book from the archive or for a blank book. It sends its
///     requests to the library console it is linked to with a network configurator.
/// </summary>
[RegisterComponent]
public sealed partial class LibraryRequestConsoleComponent : Component
{
    [DataField]
    public TimeSpan SearchCooldown = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan RequestCooldown = TimeSpan.FromSeconds(3);

    // Runtime state, server only.
    public string SearchTitle = string.Empty;
    public string SearchAuthor = string.Empty;
    public int SearchPage;

    public TimeSpan NextSearchTime;
    public TimeSpan NextRequestTime;
}

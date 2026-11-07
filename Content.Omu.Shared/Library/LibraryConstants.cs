// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Shared.Library;

/// <summary>
///     Shared limits and category names for the library archive.
///     Ported from tgstation's library (BOOKS_PER_PAGE, upload/search categories, etc).
/// </summary>
public static class LibraryConstants
{
    public const int MaxTitleLength = 30;
    public const int MaxAuthorLength = 42;
    public const int MaxBorrowerLength = 42;

    /// <summary>
    ///     Hard cap on the content length of an archived book. Paper itself is capped lower, this just
    ///     guards against abuse of hand crafted messages.
    /// </summary>
    public const int MaxContentLength = 20000;

    public const int BooksPerPage = 10;
    public const int MaxInventory = 300;

    /// <summary>Most books a book pinpointer's menu lists.</summary>
    public const int MaxPinpointerBooks = 1000;
    public const int MaxPendingBooks = 100;

    /// <summary>A registered book that was taken and not checked out becomes "missing" after this long.</summary>
    public const int MissingDelaySeconds = 60;

    /// <summary>How far from the console, in tiles, the room is searched for books.</summary>
    public const int InventoryRadius = 25;

    /// <summary>Most tiles of the console's room that are searched, so a huge open area stays cheap.</summary>
    public const int MaxInventoryTiles = 2500;

    public const int MinLoanMinutes = 1;
    public const int MaxLoanMinutes = 600;
    public const int DefaultLoanMinutes = 30;

    // Device link ports. The console has a "binder" and a "scanner" port, the binder and the scanner each have
    // a "console" port. Link them with a network configurator.
    public const string BinderSourcePort = "LibraryBinder";
    public const string ScannerSourcePort = "LibraryScanner";
    public const string ConsoleSinkPort = "LibraryConsole";

    /// <summary>The NanoChat number the library messages come from. Real cards have four digit numbers.</summary>
    public const uint NanoChatSenderNumber = 10001;

    // Book requests: a request console has a "requests" port that goes to the "requests" port of a library console.
    public const string RequestSourcePort = "LibraryRequests";
    public const string RequestSinkPort = "LibraryRequestInbox";

    /// <summary>Requests a library console keeps waiting for an answer.</summary>
    public const int MaxPendingRequests = 40;

    /// <summary>Requests one request console can have waiting at the same time.</summary>
    public const int MaxRequestsPerConsole = 5;

    /// <summary>Answered requests are kept for the requester to see, the oldest go first beyond this.</summary>
    public const int MaxResolvedRequests = 20;

    /// <summary>Search-only pseudo category that matches every book.</summary>
    public const string AnyCategory = "Any";

    /// <summary>Random archive books spread over the bookshelves of a station when its map loads.</summary>
    public const int ArchiveBooksPerStation = 20;

    public const string DefaultUploadCategory = "Fiction";

    /// <summary>Books in this category are never picked for random book spawns lying around the station.</summary>
    public const string HiddenFromSpawnsCategory = "Adult";

    public static readonly string[] UploadCategories =
    [
        "Fiction",
        "Non-fiction",
        "Adult",
        "Reference",
        "Religion",
    ];

    public static readonly string[] SearchCategories =
    [
        AnyCategory,
        "Fiction",
        "Non-fiction",
        "Adult",
        "Reference",
        "Religion",
    ];

    public static bool IsUploadCategory(string category)
    {
        return Array.IndexOf(UploadCategories, category) >= 0;
    }

    public static bool IsSearchCategory(string category)
    {
        return Array.IndexOf(SearchCategories, category) >= 0;
    }
}

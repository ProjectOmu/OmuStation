// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Content.Omu.Common.CCVar;
using Content.Omu.Shared.Library;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Library;

/// <summary>
///     The persistent library archive, the equivalent of the <c>library</c> table in tgstation.
///     Every book is stored as its own small json file in the server's user data (<c>/library/</c>), so the
///     archive survives round restarts and server restarts without needing a database migration.
/// </summary>
public sealed class LibraryArchiveSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const string GuidebookParent = "BaseGuidebook";
    private static readonly ProtoId<TagPrototype> GuidebookTag = "LibraryGuidebook";

    private static readonly ResPath ArchiveDirectory = new("/library");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Dictionary<int, LibraryArchiveEntry> _books = new();
    private int _nextId = 1;

    // Shuffle bag for random spawns, so books on a station are not repeated until all were used once.
    private readonly List<int> _bag = new();
    private List<LibraryArchiveEntry>? _fallback;

    /// <summary>
    ///     False when the archive directory could not be created or written to. Consoles show this as
    ///     "connection to the archive severed", like tgstation does when the database is down.
    /// </summary>
    public bool Available { get; private set; }

    /// <summary>
    ///     Raised whenever a book is added, deleted or restored so open consoles can refresh.
    /// </summary>
    public event Action? ArchiveChanged;

    public override void Initialize()
    {
        base.Initialize();

        Load();
        SeedMissing();
    }

    private void Load()
    {
        _books.Clear();
        _nextId = 1;

        try
        {
            _res.UserData.CreateDir(ArchiveDirectory);
        }
        catch (Exception e)
        {
            Log.Error($"Could not create the library archive directory, the library archive is unavailable: {e}");
            Available = false;
            return;
        }

        Available = true;

        foreach (var name in _res.UserData.DirectoryEntries(ArchiveDirectory))
        {
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            var path = ArchiveDirectory / name;
            try
            {
                using var reader = new StreamReader(_res.UserData.OpenRead(path));
                var entry = JsonSerializer.Deserialize<LibraryArchiveEntry>(reader.ReadToEnd(), JsonOptions);
                if (entry == null || entry.Id <= 0)
                {
                    Log.Warning($"Ignoring invalid library archive file {path}");
                    continue;
                }

                _books[entry.Id] = entry;
                _nextId = Math.Max(_nextId, entry.Id + 1);
            }
            catch (Exception e)
            {
                Log.Warning($"Could not read library archive file {path}: {e.Message}");
            }
        }

        Log.Info($"Library archive loaded with {_books.Count} books.");
    }

    /// <summary>
    ///     Adds the premade books of the <see cref="LibrarySeedPrototype"/>s that the archive does not have
    ///     yet. Books a moderator deleted stay in the archive (flagged deleted), so they are not added again.
    /// </summary>
    private void SeedMissing()
    {
        if (!Available)
            return;

        var known = new HashSet<string>(_books.Values
            .Where(b => b.SeedId != null)
            .Select(b => b.SeedId!));

        var added = 0;
        foreach (var seed in BuildSeedEntries())
        {
            if (seed.SeedId == null || !known.Add(seed.SeedId))
                continue;

            seed.Id = _nextId;
            if (!Save(seed))
                continue;

            _nextId++;
            _books[seed.Id] = seed;
            added++;
        }

        if (added > 0)
            Log.Info($"Added {added} premade books to the library archive.");
    }

    private List<LibraryArchiveEntry> BuildSeedEntries()
    {
        var result = new List<LibraryArchiveEntry>();
        foreach (var seed in _proto.EnumeratePrototypes<LibrarySeedPrototype>())
        {
            var category = LibraryConstants.IsUploadCategory(seed.Category)
                ? seed.Category
                : LibraryConstants.DefaultUploadCategory;

            foreach (var bookId in seed.Books)
            {
                if (!_proto.TryIndex(bookId, out var book) ||
                    !book.TryGetComponent<PaperComponent>(out var paper, _factory))
                {
                    Log.Warning($"Library seed {seed.ID} lists {bookId}, which is not a paper book.");
                    continue;
                }

                AddSeedEntry(result, $"book:{bookId}", book.Name, seed.Author, paper.Content, category);
            }

            foreach (var text in seed.Texts)
            {
                AddSeedEntry(result, $"text:{text.Text}", text.Title, text.Author ?? seed.Author, text.Text, category);
            }
        }

        AddGuidebookEntries(result);
        return result;
    }

    /// <summary>
    ///     Every guidebook item (anything built on <c>BaseGuidebook</c>) gets an archive entry that prints the
    ///     real guidebook, so the librarian can hand one out. They are not paper books and have no cover.
    /// </summary>
    private void AddGuidebookEntries(List<LibraryArchiveEntry> list)
    {
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || proto.ID == GuidebookParent)
                continue;

            // The tag is on BaseGuidebook, the parent chain is a second way to recognise them.
            var guide = proto.TryGetComponent<TagComponent>(out var tags, _factory) &&
                        _tag.HasTag(tags, GuidebookTag);
            foreach (var parent in _proto.EnumerateParents<EntityPrototype>(proto.ID) ?? [])
            {
                if (parent.ID == GuidebookParent)
                {
                    guide = true;
                    break;
                }
            }

            if (!guide)
                continue;

            var description = LibraryText.CleanContent(proto.Description, LibraryConstants.MaxContentLength);
            list.Add(new LibraryArchiveEntry
            {
                SeedId = $"guidebook:{proto.ID}",
                SpawnPrototype = proto.ID,
                Title = FitTitle(proto.Name),
                Author = "Nanotrasen",
                Content = string.IsNullOrWhiteSpace(description) ? proto.Name : description,
                Category = "Reference",
                UploaderUser = "library",
                UploaderCharacter = "library",
                UploadedUtc = DateTime.UtcNow,
            });
        }
    }

    private void AddSeedEntry(
        List<LibraryArchiveEntry> list,
        string seedId,
        string title,
        string author,
        string contentOrKey,
        string category)
    {
        // Premade books store a locale key as their content, resolved when they are map initialised.
        var content = Loc.TryGetString(contentOrKey, out var text) ? text : contentOrKey;
        var cleaned = LibraryText.CleanContent(content, LibraryConstants.MaxContentLength);
        if (string.IsNullOrWhiteSpace(cleaned))
            return;

        list.Add(new LibraryArchiveEntry
        {
            SeedId = seedId,
            Title = FitTitle(title),
            Author = LibraryText.CleanLine(author, LibraryConstants.MaxAuthorLength),
            Content = cleaned,
            Category = category,
            UploaderUser = "library",
            UploaderCharacter = "library",
            UploadedUtc = DateTime.UtcNow,
        });
    }

    /// <summary>
    ///     Shortens a long title at a word boundary instead of in the middle of a word.
    /// </summary>
    private static string FitTitle(string title)
    {
        var clean = LibraryText.CleanLine(title, 200);
        if (clean.Length <= LibraryConstants.MaxTitleLength)
            return clean;

        var cut = clean.LastIndexOf(' ', LibraryConstants.MaxTitleLength - 1);
        if (cut < LibraryConstants.MaxTitleLength / 2)
            cut = LibraryConstants.MaxTitleLength - 1;

        return clean.Substring(0, cut).TrimEnd() + "…";
    }

    /// <summary>
    ///     The premade books that are put on the station's bookshelves every round: the archive's seed
    ///     books that were not deleted, or the seed prototypes themselves when the archive is unavailable.
    /// </summary>
    public List<LibraryArchiveEntry> GetSeedBooks()
    {
        var books = _books.Values
            .Where(b => b.SeedId != null && !b.Deleted && b.SpawnPrototype == null)
            .OrderBy(b => b.Id)
            .ToList();
        if (books.Count > 0 || Available)
            return books;

        _fallback ??= BuildSeedEntries().Where(b => b.SpawnPrototype == null).ToList();
        return _fallback;
    }

    /// <summary>
    ///     Picks a book for a random book spawn: archive books that are not deleted and not in the
    ///     <see cref="LibraryConstants.HiddenFromSpawnsCategory"/>, every book once before any repeats.
    ///     Falls back to the premade seed books when the archive is unavailable or has nothing to offer.
    /// </summary>
    public bool TryPickRandom(out LibraryArchiveEntry entry)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            while (_bag.Count > 0)
            {
                var index = _random.Next(_bag.Count);
                var id = _bag[index];
                _bag[index] = _bag[^1];
                _bag.RemoveAt(_bag.Count - 1);

                if (_books.TryGetValue(id, out var found) && IsSpawnable(found))
                {
                    entry = found;
                    return true;
                }
            }

            foreach (var book in _books.Values)
            {
                if (IsSpawnable(book))
                    _bag.Add(book.Id);
            }

            if (_bag.Count == 0)
                break;
        }

        _fallback ??= BuildSeedEntries().Where(b => b.SpawnPrototype == null).ToList();
        if (_fallback.Count > 0)
        {
            entry = _random.Pick(_fallback);
            return true;
        }

        entry = default!;
        return false;
    }

    private static bool IsSpawnable(LibraryArchiveEntry book)
    {
        return !book.Deleted && !book.Pending && book.SpawnPrototype == null && book.Category != LibraryConstants.HiddenFromSpawnsCategory;
    }

    private bool Save(LibraryArchiveEntry entry)
    {
        if (!Available)
            return false;

        try
        {
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            using var writer = _res.UserData.OpenWriteText(ArchiveDirectory / $"{entry.Id}.json");
            writer.Write(json);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Could not write library book {entry.Id} to disk: {e}");
            return false;
        }
    }

    /// <summary>
    ///     Number of books that are not deleted.
    /// </summary>
    public int BookCount => _books.Values.Count(b => !b.Deleted && !b.Pending);

    /// <summary>
    ///     Number of uploaded books waiting for an admin to review them.
    /// </summary>
    public int PendingCount => _books.Values.Count(b => b.Pending && !b.Deleted);

    /// <summary>
    ///     Searches the archive, newest books first. Mirrors the query of the tgstation library console:
    ///     substring matches on title and author, an exact category (or <see cref="LibraryConstants.AnyCategory"/>)
    ///     and an optional id.
    /// </summary>
    /// <param name="page">Zero based page, clamped to the available pages.</param>
    /// <param name="pageCount">Zero based index of the last page.</param>
    public List<LibraryBookSummary> Search(
        string title,
        string author,
        string category,
        int? id,
        ref int page,
        out int pageCount)
    {
        var matches = _books.Values
            .Where(b => !b.Deleted && !b.Pending)
            .Where(b => id == null || b.Id == id)
            .Where(b => b.Title.Contains(title, StringComparison.OrdinalIgnoreCase))
            .Where(b => b.Author.Contains(author, StringComparison.OrdinalIgnoreCase))
            .Where(b => category == LibraryConstants.AnyCategory || b.Category == category)
            .OrderByDescending(b => b.Id)
            .ToList();

        pageCount = Math.Max(matches.Count - 1, 0) / LibraryConstants.BooksPerPage;
        page = Math.Clamp(page, 0, pageCount);

        return matches
            .Skip(page * LibraryConstants.BooksPerPage)
            .Take(LibraryConstants.BooksPerPage)
            .Select(b => new LibraryBookSummary(b.Id, b.Title, b.Author, b.Category))
            .ToList();
    }

    /// <summary>
    ///     A fingerprint of a book's text that ignores case, spacing and punctuation, so a copy of a book
    ///     with a few changed symbols still counts as the same text. Empty when the text has no letters or digits.
    /// </summary>
    public static string MakeContentKey(string content)
    {
        var builder = new StringBuilder(content.Length);
        foreach (var c in content)
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        if (builder.Length == 0)
            return string.Empty;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    ///     Looks for an archived book (not deleted) that has the same text, whatever its title or author is.
    ///     Books waiting for review count too, but their id is not given out, so <paramref name="pending"/>
    ///     tells the caller that the match is still under review.
    /// </summary>
    public bool TryFindDuplicate(string content, out int id)
    {
        return TryFindDuplicate(content, out id, out _);
    }

    public bool TryFindDuplicate(string content, out int id, out bool pending)
    {
        var key = MakeContentKey(content);
        if (key.Length > 0)
        {
            foreach (var book in _books.Values)
            {
                if (!book.Deleted && book.ContentKey == key)
                {
                    id = book.Id;
                    pending = book.Pending;
                    return true;
                }
            }
        }

        id = 0;
        pending = false;
        return false;
    }

    /// <summary>
    ///     Gets a premade book by its seed id (a guidebook, for example), if it was not deleted.
    /// </summary>
    public bool TryGetBySeed(string seedId, out LibraryArchiveEntry entry)
    {
        foreach (var book in _books.Values)
        {
            if (book.SeedId == seedId && !book.Deleted && !book.Pending)
            {
                entry = book;
                return true;
            }
        }

        entry = default!;
        return false;
    }

    /// <summary>
    ///     Gets a book that has not been deleted.
    /// </summary>
    public bool TryGet(int id, out LibraryArchiveEntry entry)
    {
        if (_books.TryGetValue(id, out var found) && !found.Deleted && !found.Pending)
        {
            entry = found;
            return true;
        }

        entry = default!;
        return false;
    }

    /// <summary>
    ///     Adds a book to the archive. Returns the new id, or null when the archive is full, unavailable or
    ///     the book could not be written. Books with <paramref name="pending"/> set are only queued for admin
    ///     review and stay invisible to players until <see cref="Approve"/> is called.
    /// </summary>
    public int? Add(
        LibraryBookData book,
        string category,
        string uploaderUser,
        string uploaderCharacter,
        LibraryCover? cover = null,
        bool pending = false)
    {
        if (!Available)
            return null;

        if (TryFindDuplicate(book.Content, out _))
            return null;

        var max = _cfg.GetCVar(OmuCVars.LibraryMaxBooks);
        if (max > 0 && BookCount + (pending ? PendingCount : 0) >= max)
            return null;

        if (pending && PendingCount >= LibraryConstants.MaxPendingBooks)
            return null;

        var entry = new LibraryArchiveEntry
        {
            Id = _nextId,
            Title = book.Title,
            Author = book.Author,
            Content = book.Content,
            Category = category,
            Cover = cover == null ? null : LibraryCovers.Sanitize(cover.Value),
            UploaderUser = uploaderUser,
            UploaderCharacter = uploaderCharacter,
            UploadedUtc = DateTime.UtcNow,
            Pending = pending,
        };

        if (!Save(entry))
            return null;

        _nextId++;
        _books[entry.Id] = entry;
        ArchiveChanged?.Invoke();
        return entry.Id;
    }

    /// <summary>
    ///     Soft deletes a book, like the <c>deleted</c> column in tgstation. The file stays on disk so a
    ///     moderator can restore it.
    /// </summary>
    public bool Delete(int id, string deletedBy)
    {
        if (!_books.TryGetValue(id, out var entry) || entry.Deleted || entry.Pending)
            return false;

        entry.Deleted = true;
        entry.DeletedBy = deletedBy;
        if (!Save(entry))
        {
            entry.Deleted = false;
            entry.DeletedBy = null;
            return false;
        }

        ArchiveChanged?.Invoke();
        return true;
    }

    public bool Restore(int id)
    {
        if (!_books.TryGetValue(id, out var entry) || !entry.Deleted)
            return false;

        entry.Deleted = false;
        var deletedBy = entry.DeletedBy;
        entry.DeletedBy = null;
        if (!Save(entry))
        {
            entry.Deleted = true;
            entry.DeletedBy = deletedBy;
            return false;
        }

        ArchiveChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     The books waiting for review, oldest first.
    /// </summary>
    public List<LibraryArchiveEntry> GetPending()
    {
        return _books.Values.Where(b => b.Pending && !b.Deleted).OrderBy(b => b.Id).ToList();
    }

    public bool TryGetPending(int id, out LibraryArchiveEntry entry)
    {
        if (_books.TryGetValue(id, out var found) && found.Pending && !found.Deleted)
        {
            entry = found;
            return true;
        }

        entry = default!;
        return false;
    }

    /// <summary>
    ///     Archive books (not pending, not deleted) for the admin tab, newest first. The filter matches the
    ///     id, title, author or the uploader's username.
    /// </summary>
    public List<LibraryArchiveEntry> SearchAdmin(string filter, ref int page, out int pageCount)
    {
        var matches = _books.Values
            .Where(b => !b.Deleted && !b.Pending)
            .Where(b => filter.Length == 0 ||
                        b.Id.ToString() == filter ||
                        b.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        b.Author.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        b.UploaderUser.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(b => b.Id)
            .ToList();

        pageCount = Math.Max(matches.Count - 1, 0) / LibraryConstants.BooksPerPage;
        page = Math.Clamp(page, 0, pageCount);
        return matches.Skip(page * LibraryConstants.BooksPerPage).Take(LibraryConstants.BooksPerPage).ToList();
    }

    /// <summary>
    ///     Accepts a pending book, which makes it visible in the archive.
    /// </summary>
    public bool Approve(int id)
    {
        if (!TryGetPending(id, out var entry))
            return false;

        var max = _cfg.GetCVar(OmuCVars.LibraryMaxBooks);
        if (max > 0 && BookCount >= max)
            return false;

        entry.Pending = false;
        if (!Save(entry))
        {
            entry.Pending = true;
            return false;
        }

        ArchiveChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     Declines a pending book and deletes it for good. If the file cannot be removed the book is hidden
    ///     (soft deleted) instead, so players never see it either way.
    /// </summary>
    public bool Decline(int id, string declinedBy)
    {
        if (!TryGetPending(id, out var entry))
            return false;

        try
        {
            _res.UserData.Delete(ArchiveDirectory / $"{id}.json");
        }
        catch (Exception e)
        {
            Log.Error($"Could not delete declined library book {id}, hiding it instead: {e}");
            entry.Pending = false;
            entry.Deleted = true;
            entry.DeletedBy = declinedBy;
            Save(entry);
        }

        _books.Remove(id);
        ArchiveChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     Every book, including deleted ones, newest first. For admin tooling.
    /// </summary>
    public IEnumerable<LibraryArchiveEntry> All()
    {
        return _books.Values.OrderByDescending(b => b.Id);
    }

    public bool TryGetAny(int id, out LibraryArchiveEntry entry)
    {
        return _books.TryGetValue(id, out entry!);
    }
}

/// <summary>
///     A book as stored on disk.
/// </summary>
public sealed class LibraryArchiveEntry
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    private string? _contentKey;

    /// <summary>Fingerprint of <see cref="Content"/>, see <see cref="LibraryArchiveSystem.MakeContentKey"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ContentKey => _contentKey ??= LibraryArchiveSystem.MakeContentKey(Content);

    public string Category { get; set; } = LibraryConstants.DefaultUploadCategory;

    /// <summary>Username (ckey) of the player that uploaded the book.</summary>
    public string UploaderUser { get; set; } = string.Empty;

    /// <summary>Name of the character that uploaded the book.</summary>
    public string UploaderCharacter { get; set; } = string.Empty;

    public DateTime UploadedUtc { get; set; }
    public bool Deleted { get; set; }

    /// <summary>True while an admin has not accepted the book yet. Pending books are hidden from players.</summary>
    public bool Pending { get; set; }

    /// <summary>
    ///     How the book looks. Books without one (premade books, books uploaded before covers existed)
    ///     use <see cref="LibraryCovers.Default"/> of their id.
    /// </summary>
    public LibraryCover? Cover { get; set; }

    /// <summary>Set for premade books copied from a <see cref="LibrarySeedPrototype"/>.</summary>
    public string? SeedId { get; set; }

    /// <summary>
    ///     When set, printing this entry spawns that item (a guidebook) instead of a paper book with text.
    ///     Such entries never spawn as random books or on shelves.
    /// </summary>
    public string? SpawnPrototype { get; set; }

    public string? DeletedBy { get; set; }
}

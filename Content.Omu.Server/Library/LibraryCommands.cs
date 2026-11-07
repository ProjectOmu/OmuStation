// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Omu.Shared.Library;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Omu.Server.Library;

/// <summary>
///     Moderation tooling for the library archive, the equivalent of tgstation's library admin panel.
/// </summary>
[AdminCommand(AdminFlags.Moderator)]
public sealed class LibraryListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly LibraryArchiveSystem _archive = default!;

    public override string Command => "librarylist";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var search = string.Join(' ', args);

        var books = _archive.All()
            .Where(b => search.Length == 0 ||
                        b.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        b.Author.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        b.UploaderUser.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();

        if (books.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-librarylist-none"));
            return;
        }

        foreach (var book in books)
        {
            shell.WriteLine(Loc.GetString("cmd-librarylist-entry",
                ("id", book.Id),
                ("title", book.Title),
                ("author", book.Author),
                ("category", book.Category),
                ("uploader", book.UploaderUser),
                ("deleted", book.Deleted),
                ("pending", book.Pending)));
        }
    }
}

[AdminCommand(AdminFlags.Moderator)]
public sealed class LibraryDeleteCommand : LocalizedEntityCommands
{
    [Dependency] private readonly LibraryArchiveSystem _archive = default!;

    public override string Command => "librarydelete";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var id))
        {
            shell.WriteError(Loc.GetString("cmd-librarydelete-help"));
            return;
        }

        var by = shell.Player?.Name ?? "server";
        shell.WriteLine(_archive.Delete(id, by)
            ? Loc.GetString("cmd-librarydelete-success", ("id", id))
            : Loc.GetString("cmd-librarydelete-failure", ("id", id)));
    }
}

[AdminCommand(AdminFlags.Moderator)]
public sealed class LibraryRestoreCommand : LocalizedEntityCommands
{
    [Dependency] private readonly LibraryArchiveSystem _archive = default!;

    public override string Command => "libraryrestore";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var id))
        {
            shell.WriteError(Loc.GetString("cmd-libraryrestore-help"));
            return;
        }

        shell.WriteLine(_archive.Restore(id)
            ? Loc.GetString("cmd-libraryrestore-success", ("id", id))
            : Loc.GetString("cmd-libraryrestore-failure", ("id", id)));
    }
}

/// <summary>
///     Lists every bookshelf on the station with its position and the number of books in it.
/// </summary>
[AdminCommand(AdminFlags.Moderator)]
public sealed class LibraryShelvesCommand : LocalizedEntityCommands
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override string Command => "libraryshelves";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var shelves = 0;
        var books = 0;

        var query = EntityManager.EntityQueryEnumerator<ArchiveBookshelfComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var count = 0;
            if (EntityManager.TryGetComponent(uid, out Content.Shared.Storage.StorageComponent? storage))
                count = storage.StoredItems.Count;

            var position = _transform.GetMapCoordinates(uid, xform);
            shell.WriteLine(Loc.GetString("cmd-libraryshelves-entry",
                ("uid", EntityManager.GetNetEntity(uid).ToString()),
                ("map", position.MapId.ToString()),
                ("x", (int) position.X),
                ("y", (int) position.Y),
                ("books", count)));

            shelves++;
            books += count;
        }

        shell.WriteLine(Loc.GetString("cmd-libraryshelves-total", ("shelves", shelves), ("books", books)));
    }
}

/// <summary>
///     Puts the round start library books on the station's shelves again, for testing or after an admin
///     changed the archive. Books already on the shelves stay.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class LibraryStockShelvesCommand : LocalizedEntityCommands
{
    [Dependency] private readonly ArchiveBookshelfSystem _shelves = default!;

    public override string Command => "librarystockshelves";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_shelves.StockStations(out var stations, out var shelves, out var books))
        {
            shell.WriteError(Loc.GetString("cmd-librarystockshelves-none"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-librarystockshelves-success",
            ("books", books), ("shelves", shelves), ("stations", stations)));
    }
}

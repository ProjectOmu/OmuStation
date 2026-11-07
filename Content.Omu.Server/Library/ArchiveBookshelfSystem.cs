// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using Content.Server.GameTicking;
using Content.Shared.Station.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Random;

namespace Content.Omu.Server.Library;

/// <summary>
///     At round start, puts every premade library book plus a number of random archive books on the
///     bookshelves of each station. The books are spread over all the shelves, not added to every shelf.
/// </summary>
public sealed class ArchiveBookshelfSystem : EntitySystem
{
    private const string PremadeBookPrototype = "BookLibraryPrinted";
    private const string RandomBookPrototype = "BookRandomArchive";

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LibraryArchiveSystem _archive = default!;
    [Dependency] private readonly RandomArchiveBookSystem _fill = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Raised once the maps are initialized, so shelves and their storage are fully set up.
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
            return;

        StockStations(out _, out _, out _);
    }

    /// <summary>
    ///     Puts the books on the shelves of every station. Returns false when there are no shelves.
    /// </summary>
    public bool StockStations(out int stations, out int shelves, out int books)
    {
        stations = 0;
        shelves = 0;
        books = 0;

        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var station, out var data))
        {
            var found = FindShelves(data.Grids);
            if (found.Count == 0)
                continue;

            var placed = Stock(found);
            Log.Info($"Put {placed} library books on {found.Count} bookshelves of {ToPrettyString(station)}.");

            stations++;
            shelves += found.Count;
            books += placed;
        }

        return stations > 0;
    }

    public List<EntityUid> FindShelves(IReadOnlySet<EntityUid> grids)
    {
        var shelves = new List<EntityUid>();
        var query = EntityQueryEnumerator<ArchiveBookshelfComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is { } grid && grids.Contains(grid))
                shelves.Add(uid);
        }

        return shelves;
    }

    private int Stock(List<EntityUid> shelves)
    {
        var placed = 0;
        var coordinates = Transform(shelves[0]).Coordinates;

        // Every premade book (stories, essays, the Ian adventures...) is always on the station.
        foreach (var entry in _archive.GetSeedBooks())
        {
            var book = Spawn(PremadeBookPrototype, coordinates);
            _fill.Fill(book, entry);
            if (!TryPlace(book, shelves))
                break;

            placed++;
        }

        // Plus random books from the archive, which includes whatever players uploaded.
        for (var i = 0; i < LibraryConstants.ArchiveBooksPerStation; i++)
        {
            var book = Spawn(RandomBookPrototype, coordinates);
            if (!TryPlace(book, shelves))
                break;

            placed++;
        }

        return placed;
    }

    private bool TryPlace(EntityUid book, List<EntityUid> shelves)
    {
        // A random shelf first, then the others in order if it is full.
        var start = _random.Next(shelves.Count);
        for (var i = 0; i < shelves.Count; i++)
        {
            var shelf = shelves[(start + i) % shelves.Count];
            if (_storage.Insert(shelf, book, out _, playSound: false))
                return true;
        }

        // Every shelf is full, don't leave the book lying on the floor.
        QueueDel(book);
        return false;
    }
}

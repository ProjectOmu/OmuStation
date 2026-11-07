// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Server.Entities.Heretic;
using Content.Omu.Shared.Library;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Library;

/// <summary>
///     Finds library books for the console and the book pinpointers: the books of the room somebody or something
///     stands in, or every book on the station.
/// </summary>
public sealed class LibraryRoomSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> BookTag = "Book";
    private static readonly ProtoId<TagPrototype> GuidebookTag = "LibraryGuidebook";
    private const string GuidebookParent = "BaseGuidebook";

    /// <summary>
    ///     Paper books and guidebooks count as library books. Heretic tomes are not library books, even though
    ///     they are books too.
    /// </summary>
    public bool IsLibraryBook(EntityUid uid)
    {
        if (!_tag.HasTag(uid, BookTag) || HasComp<HereticTomeComponent>(uid))
            return false;

        return HasComp<PaperComponent>(uid) || IsGuidebook(uid);
    }

    public bool IsGuidebook(EntityUid uid)
    {
        if (_tag.HasTag(uid, GuidebookTag))
            return true;

        if (MetaData(uid).EntityPrototype is not { } proto)
            return false;

        if (proto.ID == GuidebookParent)
            return true;

        foreach (var parent in _proto.EnumerateParents<EntityPrototype>(proto.ID) ?? [])
        {
            if (parent.ID == GuidebookParent)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Books in somebody's hands or bag.
    /// </summary>
    public bool IsCarried(EntityUid book)
    {
        var current = book;
        while (_container.TryGetContainingContainer(current, out var container))
        {
            current = container.Owner;
            if (HasComp<MobStateComponent>(current))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     The library books lying in the room of <paramref name="origin"/>: loose books, and the books inside
    ///     shelves, crates and lockers, which are found by walking the containers of what lies in the room.
    ///     Books somebody carries are left out, unless <paramref name="includeCarried"/> is set.
    /// </summary>
    public List<EntityUid> FindRoomBooks(EntityUid origin, bool includeCarried = false)
    {
        var seen = new HashSet<EntityUid>();
        var books = new List<EntityUid>();
        var stack = new Stack<EntityUid>(GetRoomEntities(origin));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
                continue;

            if (IsLibraryBook(current) && (includeCarried || !IsCarried(current)))
                books.Add(current);

            if (!HasComp<ContainerManagerComponent>(current))
                continue;

            foreach (var container in _container.GetAllContainers(current))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    stack.Push(contained);
                }
            }
        }

        return books;
    }

    /// <summary>
    ///     Every library book on the same grid (the station) as <paramref name="origin"/>, wherever it is, even
    ///     in somebody's hands or in a locker.
    /// </summary>
    public List<EntityUid> FindStationBooks(EntityUid origin)
    {
        var books = new List<EntityUid>();
        var grid = Transform(origin).GridUid;
        var map = Transform(origin).MapID;

        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (grid != null ? xform.GridUid != grid : xform.MapID != map)
                continue;

            if (IsLibraryBook(uid))
                books.Add(uid);
        }

        return books;
    }

    /// <summary>
    ///     Everything lying in the room <paramref name="origin"/> is in. The room is found by spreading out over the
    ///     floor from there, like air does: walls, full-tile windows and airlocks stop it, directional windows and
    ///     windoors do not. So a console sees the books of its own library and not those of the room behind a wall.
    /// </summary>
    public List<EntityUid> GetRoomEntities(EntityUid origin)
    {
        var result = new List<EntityUid>();
        var xform = Transform(origin);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return result;

        // Contained things (a pinpointer in a hand) are placed by where their holder stands.
        var start = _map.TileIndicesFor(gridUid, grid, _transform.GetMoverCoordinates(origin, xform));
        var visited = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);

        var onTile = new HashSet<EntityUid>();
        Span<Vector2i> steps = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();

            // Open space has no floor, a room ends there.
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            onTile.Clear();
            _lookup.GetLocalEntitiesIntersecting(
                gridUid,
                tile,
                onTile,
                -0.05f,
                LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries);

            var blocked = false;
            foreach (var entity in onTile)
            {
                if (BlocksRoom(entity))
                {
                    blocked = true;
                    break;
                }
            }

            // The blocking tile is the wall of the room, what is on it does not belong to the room.
            if (blocked)
                continue;

            result.AddRange(onTile);

            foreach (var step in steps)
            {
                var next = tile + step;

                if (Math.Abs(next.X - start.X) > LibraryConstants.InventoryRadius ||
                    Math.Abs(next.Y - start.Y) > LibraryConstants.InventoryRadius ||
                    visited.Count >= LibraryConstants.MaxInventoryTiles ||
                    !visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        return result;
    }

    /// <summary>
    ///     Whether an entity closes off the room it stands in. It must block air in every direction, like walls,
    ///     full-tile windows and airlocks (an open airlock too, a door in the doorway still ends the room).
    ///     Directional windows and windoors only block some directions, so they do not count.
    /// </summary>
    private bool BlocksRoom(EntityUid entity)
    {
        return TryComp<AirtightComponent>(entity, out var airtight) &&
               Transform(entity).Anchored &&
               airtight.InitialAirBlockedDirection == (int) AtmosDirection.All;
    }
}

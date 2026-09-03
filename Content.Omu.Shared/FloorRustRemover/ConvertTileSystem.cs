// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.FloorRustRemover;

public sealed class ConvertTileSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConvertTileComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ConvertTileComponent, ConvertTileDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<ConvertTileComponent> floorCleaner, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Handled || args.Target != null)
            return;

        var gridUid = _transform.GetGrid(args.ClickLocation);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        var tileRef = _map.GetTileRef(gridUid.Value, mapGrid, args.ClickLocation);
        var tileDef = _turf.GetContentTileDefinition(tileRef);

        if (!floorCleaner.Comp.TileReactions.TryGetValue(tileDef.ID, out var newTileId))
            return;

        var doAfterEvent = new ConvertTileDoAfterEvent(GetNetEntity(gridUid.Value), tileRef.GridIndices, newTileId);
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, floorCleaner.Comp.Delay, doAfterEvent, floorCleaner, used: floorCleaner)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BlockDuplicate = false,
            CancelDuplicate = false,
            BreakOnMove = true,
            MovementThreshold = 0.01f,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(Entity<ConvertTileComponent> floorCleaner, ref ConvertTileDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var gridUid = GetEntity(args.GridNetUid);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        var tileRef = _map.GetTileRef(gridUid, mapGrid, args.TileIndices);
        var tileDef = _turf.GetContentTileDefinition(tileRef);

        if (!floorCleaner.Comp.TileReactions.TryGetValue(tileDef.ID, out var newTileId))
            return;

        var newTileDef = _tileDefinitionManager[newTileId];
        _entityManager.System<SharedMapSystem>().SetTile(tileRef.GridUid, mapGrid, tileRef.GridIndices, new Tile(newTileDef.TileId));
    }
}

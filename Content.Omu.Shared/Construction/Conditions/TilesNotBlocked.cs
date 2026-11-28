// SPDX-License-Identifier: MIT

using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Omu.Shared.Construction.Conditions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class TilesNotBlocked : IConstructionCondition
{
    private static readonly TileNotBlocked Tile = new();

    [DataField(required: true)]
    public List<Vector2i> Tiles = [];

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var rotation = direction.ToAngle();
        foreach (var tile in Tiles)
        {
            if (!Tile.Condition(user, location.Offset(rotation.RotateVec(tile)), direction))
                return false;
        }

        return true;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return Tile.GenerateGuideEntry();
    }
}

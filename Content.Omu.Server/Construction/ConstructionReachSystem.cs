// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Omu.Common.Construction;
using Content.Omu.Shared.Construction.Conditions;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Construction;

public sealed class ConstructionReachSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConstructionReachEvent>(OnReach);
    }

    private void OnReach(ref ConstructionReachEvent args)
    {
        if (!_prototype.TryIndex<ConstructionPrototype>(args.Prototype, out var construction)
            || construction.Conditions.OfType<TilesNotBlocked>().FirstOrDefault() is not { } footprint
            || footprint.Tiles.Count == 0)
        {
            return;
        }

        var user = _transform.GetMapCoordinates(args.User).Position;
        var rotation = args.Rotation.GetCardinalDir().ToAngle();
        var target = args.Target;
        args.Target = footprint.Tiles
            .Select(tile => target.Offset(rotation.RotateVec(tile)))
            .MinBy(spot => (_transform.ToMapCoordinates(spot).Position - user).LengthSquared());
    }
}

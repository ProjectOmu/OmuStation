// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Omu.Common.CCVar;
using Content.Omu.Shared.Crayon;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Crayon;
using Content.Shared.Decals;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.SprayPainter.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Omu.Client.Decals;

public sealed class DecalPreviewOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly SharedChargesSystem _charges;
    private readonly SharedHandsSystem _hands;
    private readonly SharedInteractionSystem _interaction;
    private readonly SharedMapSystem _map;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;
    private readonly TurfSystem _turf;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public DecalPreviewOverlay()
    {
        IoCManager.InjectDependencies(this);

        _charges = _entity.System<SharedChargesSystem>();
        _hands = _entity.System<SharedHandsSystem>();
        _interaction = _entity.System<SharedInteractionSystem>();
        _map = _entity.System<SharedMapSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<SharedTransformSystem>();
        _turf = _entity.System<TurfSystem>();

        ZIndex = 1000;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eye.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var opacity = _cfg.GetCVar(OmuCVars.DecalPreviewOpacity);
        if (opacity <= 0f
            || _player.LocalEntity is not { } user
            || _hands.GetActiveItem(user) is not { } held
            || GetPreview(held) is not { } preview
            || !_prototype.TryIndex(preview.Decal, out var decal))
            return;

        var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mousePos.MapId != args.MapId
            || !_mapManager.TryFindGridAt(mousePos, out var gridUid, out var grid)
            || _turf.IsSpace(_map.GetTileRef(gridUid, grid, mousePos))
            || !_interaction.InRangeUnobstructed(user, mousePos))
            return;

        var localPos = Vector2.Transform(mousePos.Position, _transform.GetInvWorldMatrix(gridUid));
        if (preview.Snap)
            localPos = localPos.Floored() + grid.TileSizeHalfVector;

        var box = new Box2Rotated(Box2.UnitCentered.Translated(localPos), preview.Rotation, localPos);

        var handle = args.WorldHandle;
        handle.SetTransform(_transform.GetWorldMatrix(gridUid));
        handle.DrawTextureRect(_sprite.Frame0(decal.Sprite), box, preview.Color * Color.White.WithAlpha(opacity));
        handle.SetTransform(Matrix3x2.Identity);
    }

    private DecalPreview? GetPreview(EntityUid held)
    {
        if (_entity.TryGetComponent<CrayonComponent>(held, out var crayon))
        {
            if (_charges.IsEmpty(held))
                return null;

            var rotation = _entity.TryGetComponent<CrayonRotationComponent>(held, out var rotator)
                ? rotator.Rotation
                : Angle.Zero;
            return new DecalPreview(crayon.SelectedState, crayon.Color, rotation, false);
        }

        if (!_entity.TryGetComponent<SprayPainterComponent>(held, out var painter)
            || painter.DecalMode != DecalPaintMode.Add
            || painter.ColorPickerEnabled
            || _entity.TryGetComponent<LimitedChargesComponent>(held, out var charges)
            && _charges.GetCurrentCharges((held, charges)) < painter.DecalChargeCost)
            return null;

        return new DecalPreview(painter.SelectedDecal,
            painter.SelectedDecalColor ?? Color.White,
            Angle.FromDegrees(painter.SelectedDecalAngle),
            painter.SnapDecals);
    }

    private readonly record struct DecalPreview(
        ProtoId<DecalPrototype> Decal,
        Color Color,
        Angle Rotation,
        bool Snap);
}

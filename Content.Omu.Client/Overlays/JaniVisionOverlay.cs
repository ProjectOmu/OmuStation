// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Client.Light.Visualizers;
using Content.Omu.Shared.Overlays;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Content.Shared.Light;
using Content.Shared.Stealth.Components;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Omu.Client.Overlays;

public sealed class JaniVisionOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;

    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly ContainerSystem _container;
    private readonly SharedSolutionContainerSystem _solutionContainerSystem;
    private readonly SharedAppearanceSystem _sharedAppearanceSystem;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly List<JaniVisionRenderEntry> _entries = [];

    public JaniVisionComponent? Comp;

    public JaniVisionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _transform = _entity.System<TransformSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _solutionContainerSystem = _entity.System<SharedSolutionContainerSystem>();
        _sharedAppearanceSystem = _entity.System<SharedAppearanceSystem>();

        ZIndex = -1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eyeManager.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null || Comp is null)
            return;

        var worldHandle = args.WorldHandle;
        var eye = args.Viewport.Eye;

        if (eye == null)
            return;

        var player = _player.LocalEntity;

        if (!_entity.TryGetComponent(player, out TransformComponent? _))
            return;

        var mapId = eye.Position.MapId;
        var eyePos = eye.Position;
        var eyeRot = eye.Rotation;
        var distanceCheckMax = Comp.DistanceCheckMax * Comp.DistanceCheckMax;
        var distanceCheckMin = Comp.DistanceCheckMin * Comp.DistanceCheckMin;

        _entries.Clear();
        GatherVisiblePuddleEntries(mapId, eyeRot);
        GatherDestroyedLightsEntries(mapId, eyeRot, Comp);
        GatherTrashEntries(mapId, eyeRot, Comp);

        foreach (var entry in _entries)
        {
            var entryPos = _transform.GetMapCoordinates(entry.Ent.Comp2);
            var entryDistance = Vector2.Distance(eyePos.Position, entryPos.Position);

            var entryTween = Math.Clamp(InverseLerp(distanceCheckMin, distanceCheckMax, entryDistance), 0f, 1f);
            var entryColor = entry.Color.WithAlpha(float.Lerp(Comp.DistanceAlpha, 1f, entryTween));

            Render(entry.Ent, entry.Map, worldHandle, entry.EyeRot, entryColor, Comp.JaniShader);
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void GatherVisiblePuddleEntries(MapId mapId, Angle eyeRot)
    {
        var entities = _entity.EntityQueryEnumerator<PuddleComponent, SpriteComponent, TransformComponent>();
        while (entities.MoveNext(out var uid, out var puddle, out var sprite, out var xform))
        {
            if (!CanSee(uid, sprite))
                continue;

            var entity = uid;

            if (_container.TryGetOuterContainer(uid, xform, out var container))
            {
                var owner = container.Owner;
                if (_entity.TryGetComponent<PuddleComponent>(owner, out var ownerPuddle)
                    && _entity.TryGetComponent<SpriteComponent>(owner, out var ownerSprite)
                    && _entity.TryGetComponent<TransformComponent>(owner, out var ownerXform))
                {
                    entity = owner;
                    puddle = ownerPuddle;
                    sprite = ownerSprite;
                    xform = ownerXform;
                }
            }

            if (_entries.Any(e => e.Ent.Owner == entity))
                continue;

            var color = Color.White;
            if (_solutionContainerSystem.ResolveSolution(entity, puddle.SolutionName, ref puddle.Solution, out var solution))
                color = solution.GetColor(_protoMan);

            _entries.Add(new JaniVisionRenderEntry((entity, sprite, xform), color, mapId, eyeRot));
        }
    }

    private void GatherDestroyedLightsEntries(MapId mapId, Angle eyeRot, JaniVisionComponent comp)
    {
        var entities = _entity.EntityQueryEnumerator<PoweredLightVisualsComponent, AppearanceComponent, SpriteComponent, TransformComponent>();
        while (entities.MoveNext(out var uid, out _, out _, out var sprite, out var xform))
        {
            if (!CanSee(uid, sprite))
                continue;

            var entity = uid;

            if (_container.TryGetOuterContainer(uid, xform, out var container))
            {
                var owner = container.Owner;
                if (_entity.HasComponent<PoweredLightVisualsComponent>(owner)
                    && _entity.HasComponent<AppearanceComponent>(owner)
                    && _entity.TryGetComponent<SpriteComponent>(owner, out var ownerSprite)
                    && _entity.TryGetComponent<TransformComponent>(owner, out var ownerXform))
                {
                    entity = owner;
                    sprite = ownerSprite;
                    xform = ownerXform;
                }
            }

            if (_entries.Any(e => e.Ent.Owner == entity))
                continue;

            if (!_sharedAppearanceSystem.TryGetData(entity, PoweredLightVisuals.BulbState, out PoweredLightState bulbState))
                continue;

            if (!comp.ShowAllLights && bulbState is PoweredLightState.On or PoweredLightState.Off)
                continue;

            var color = bulbState switch
            {
                PoweredLightState.Broken => comp.BrokenLightsColor,
                PoweredLightState.Burned => comp.BurnedLightsColor,
                PoweredLightState.Empty => comp.EmptyLightsColor,
                _ => comp.WorkingLightsColor,
            };

            _entries.Add(new JaniVisionRenderEntry((entity, sprite, xform), color, mapId, eyeRot));
        }
    }

    private void GatherTrashEntries(MapId mapId, Angle eyeRot, JaniVisionComponent comp)
    {
        var tags = _entity.System<TagSystem>();

        var entities = _entity.EntityQueryEnumerator<TagComponent, SpriteComponent, TransformComponent>();
        while (entities.MoveNext(out var uid, out var trash, out var sprite, out var xform))
        {
            if (!CanSee(uid, sprite))
                continue;

            var entity = uid;

            if (_container.TryGetOuterContainer(uid, xform, out var container))
            {
                var owner = container.Owner;
                if (_entity.TryGetComponent<TagComponent>(owner, out var ownerTrash)
                    && _entity.TryGetComponent<SpriteComponent>(owner, out var ownerSprite)
                    && _entity.TryGetComponent<TransformComponent>(owner, out var ownerXform))
                {
                    entity = owner;
                    trash = ownerTrash;
                    sprite = ownerSprite;
                    xform = ownerXform;
                }
            }

            if (_entries.Any(e => e.Ent.Owner == entity))
                continue;

            Color? color = null;

            foreach (var (tag, tagColor) in comp.TagColors)
            {
                if (tags.HasTag(trash, tag))
                {
                    color = tagColor;
                    break;
                }
            }

            if (!color.HasValue)
                continue;

            _entries.Add(new JaniVisionRenderEntry((entity, sprite, xform), color.Value, mapId, eyeRot));
        }
    }


    private void Render(Entity<SpriteComponent, TransformComponent> ent,
        MapId? map,
        DrawingHandleWorld handle,
        Angle eyeRot,
        Color color,
        string? shader)
    {
        var (uid, sprite, xform) = ent;
        if (xform.MapID != map || !CanSee(uid, sprite))
            return;

        var position = _transform.GetWorldPosition(xform);
        var rotation = _transform.GetWorldRotation(xform);

        var originalColor = sprite.Color;
        _sprite.SetColor((uid, sprite), color);
        if (shader != null)
            handle.UseShader(_protoMan.Index<ShaderPrototype>(shader).Instance());

        _sprite.RenderSprite((uid, sprite), handle, eyeRot, rotation, position);
        _sprite.SetColor((uid, sprite), originalColor);
        handle.UseShader(null);
    }

    private bool CanSee(EntityUid uid, SpriteComponent sprite)
    {
        return sprite.Visible && (!_entity.TryGetComponent(uid, out StealthComponent? stealth) ||
                                  !stealth.ThermalsImmune); // Goobstation - thermals ability to see invisible entities
    }

    private static float InverseLerp(float a, float b, float value)
    {
        if (Math.Abs(a - b) < 0.0001f)
            return 0f;

        return (value - a) / (b - a);
    }
}

public record struct JaniVisionRenderEntry(
    Entity<SpriteComponent, TransformComponent> Ent,
    Color Color,
    MapId? Map,
    Angle EyeRot);

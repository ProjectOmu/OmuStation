// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Omu.Shared.Crewlet;
using Content.Shared.Anomaly.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Omu.Client.Crewlet;

public sealed class CrewletDisguiseSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string AuraShader = "unshaded";

    private readonly List<PrototypeLayerData> _aura = new();

    private EntityQuery<AnomalyComponent> _anomalyQuery;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<CrewletComponent> _crewletQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;

    private bool _stale;

    public override void Initialize()
    {
        base.Initialize();

        _anomalyQuery = GetEntityQuery<AnomalyComponent>();
        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _crewletQuery = GetEntityQuery<CrewletComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<CrewletDisguiseComponent, ComponentStartup>(OnChanged);
        SubscribeLocalEvent<CrewletDisguiseComponent, AfterAutoHandleStateEvent>(OnChanged);
        SubscribeLocalEvent<CrewletDisguiseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CrewletHuskComponent, ComponentStartup>(OnChanged);
        SubscribeLocalEvent<CrewletHuskComponent, EntInsertedIntoContainerMessage>(OnChanged);
        SubscribeLocalEvent<CrewletHuskComponent, EntRemovedFromContainerMessage>(OnChanged);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_stale)
            return;

        _stale = false;
        using var query = EntityQueryEnumerator<CrewletDisguiseComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var disguise, out var sprite))
        {
            Disguise((uid, disguise, sprite));
        }
    }

    private void OnChanged<TComp, TEvent>(Entity<TComp> ent, ref TEvent args) where TComp : IComponent
    {
        _stale = true;
    }

    private void Disguise(Entity<CrewletDisguiseComponent, SpriteComponent> ent)
    {
        if (ent.Comp1.Husk is not { } husk
            || !_spriteQuery.TryComp(husk, out var huskSprite)
            || !_crewletQuery.TryComp(husk, out var crewlet))
            return;

        _sprite.CopySprite((husk, huskSprite), (ent, ent.Comp2));
        if (!_anomalyQuery.HasComp(ent))
        {
            _sprite.SetScale((ent, ent.Comp2), crewlet.Scale);
            _sprite.SetColor((ent, ent.Comp2), ent.Comp1.Color);
            return;
        }

        _sprite.SetScale((ent, ent.Comp2), Vector2.One);
        _sprite.SetColor((ent, ent.Comp2), Color.White);
        _aura.Clear();
        foreach (var spriteLayer in ent.Comp2.AllLayers)
        {
            if (spriteLayer is not SpriteComponent.Layer layer)
                continue;

            if (layer.Visible && layer.ActualRsi != null && layer.State.IsValid)
            {
                var data = layer.ToPrototypeData();
                data.Color = ent.Comp1.AuraColor;
                data.Shader = AuraShader;
                data.Scale = layer.Scale * ent.Comp1.AuraScale;
                _aura.Add(data);
            }

            _sprite.LayerSetColor(layer, layer.Color * ent.Comp1.Color);
        }

        for (var i = 0; i < _aura.Count; i++)
        {
            _sprite.AddLayer((ent, ent.Comp2), _aura[i], i);
        }

        _aura.Clear();
    }

    private void OnShutdown(Entity<CrewletDisguiseComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent)
            || !_spriteQuery.TryComp(ent, out var sprite)
            || Prototype(ent) is not { } prototype
            || !prototype.TryGetComponent(out SpriteComponent? original, Factory))
            return;

        _sprite.CopySprite((EntityUid.Invalid, original), (ent, sprite));
        if (_appearanceQuery.TryComp(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
    }
}

using Content.Shared._DV.Body.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Robust.Client.GameObjects;

namespace Content.Client._DV.Body;

/// <summary>
/// Colors feathers.
/// </summary>
public sealed class FeatherVisualizer : VisualizerSystem<FeatherComponent>
{
    // Omu begin
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FeatherComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals, after: [typeof(ClothingSystem)]);
    }

    private void OnGetEquipmentVisuals(Entity<FeatherComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!AppearanceSystem.TryGetData<Color>(ent, FeatherVisuals.FeatherColor, out var featherColor))
            return;

        foreach (var (_, layerData) in args.Layers)
        {
            if (layerData.MapKeys != null && layerData.MapKeys.Contains(ent.Comp.LayerName))
                layerData.Color = featherColor;
        }
    }
    // Omu end

    protected override void OnAppearanceChange(EntityUid uid, FeatherComponent component, ref AppearanceChangeEvent args)
    {
        if (!AppearanceSystem.TryGetData<Color>(uid, FeatherVisuals.FeatherColor, out var featherColor, args.Component))
            return;

        SpriteSystem.LayerSetColor(uid, FeatherVisualLayers.Feather, featherColor);

        if (!AppearanceSystem.TryGetData<Color>(uid, FeatherVisuals.BloodColor, out var bloodColor, args.Component))
            return;

        SpriteSystem.LayerSetColor(uid, FeatherVisualLayers.Blood, bloodColor);
    }
}

public enum FeatherVisualLayers : byte
{
    Feather,
    Blood,
}

using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Traits.Plantigrade;

public sealed class PlantigradeTraitSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantigradeTraitComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<PlantigradeTraitComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out HumanoidAppearanceComponent? appearance))
            return;

        UpdateSpriteLayers(ent, appearance);

        RemoveMarkingDisplacements(ent, appearance);

        RemoveInventoryDisplacements(ent);

        Dirty(ent.Owner, appearance);
    }

    private void RemoveInventoryDisplacements(Entity<PlantigradeTraitComponent> ent)
    {
        if (!TryComp(ent, out InventoryComponent? inventory))
            return;

        foreach (var slot in ent.Comp.InventorySlots)
        {
            inventory.Displacements.Remove(slot);
            inventory.FemaleDisplacements.Remove(slot);
            inventory.MaleDisplacements.Remove(slot);
        }

        Dirty(ent.Owner, inventory);

        var ev = new InventoryTemplateUpdated();
        RaiseLocalEvent(ent, ref ev);
    }

    private void RemoveMarkingDisplacements(Entity<PlantigradeTraitComponent> ent, HumanoidAppearanceComponent appearance)
    {
        foreach (var compSpriteLayerMapping in ent.Comp.DisplacementLayers)
        {
            appearance.MarkingsDisplacement.Remove(compSpriteLayerMapping);
        }

        Dirty(ent.Owner, appearance);
    }

    private void UpdateSpriteLayers(Entity<PlantigradeTraitComponent> ent, HumanoidAppearanceComponent appearance)
    {
        foreach (var (layer, spriteId) in ent.Comp.SpriteLayerMappings)
        {
            var targetSpriteId = spriteId;
            var morphedSprite = HumanoidVisualLayersExtension.GetSexMorph(layer, appearance.Sex, spriteId);

            if (_proto.HasIndex(morphedSprite))
                targetSpriteId = morphedSprite;

            if (_proto.HasIndex(targetSpriteId))
                _humanoid.SetBaseLayerId(ent.Owner, layer, targetSpriteId, humanoid: appearance);
        }

        Dirty(ent.Owner, appearance);
    }
}

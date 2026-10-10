using Content.Server._EinsteinEngines.Language;
using Content.Shared._EinsteinEngines.Language.Systems;

namespace Content.Server._Starlight.Traits.Assorted;

public sealed partial class XenosocializedTraitSystem : EntitySystem // Talita heartlocket gif
{
    [Dependency] private readonly LanguageSystem _languages = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenosocializedTraitComponent, ComponentInit>(OnSpawn);
        // TraitSystem adds it after PlayerSpawnCompleteEvent so it's fine.
    }

    private void OnSpawn(Entity<XenosocializedTraitComponent> entity, ref ComponentInit args)
    {
        if (!TryComp<LanguageKnowledgeComponent>(entity, out _))
        {
            Log.Warning($"Entity {entity.Owner} does not have a LanguageKnowledge but has a XenosocializedTrait!");
            return;
        }

        var metadata = MetaData(entity);
        if (metadata.EntityPrototype == null)
        {
            Log.Warning($"Entity {entity.Owner} does not have an EntityPrototype?!");
            return;
        }

        if (!metadata.EntityPrototype.Components.TryGetComponent("LanguageKnowledge", out var component))
        {
            Log.Warning($"Entity {entity.Owner}'s prototype does not have a LanguageKnowledgeComponent?!");
            return;
        }

        var prototypeLanguages = ((LanguageKnowledgeComponent) component).SpokenLanguages;

        // Byrd begin
        if (prototypeLanguages.Count <= 1)
        {
            Log.Warning($"Entity {entity.Owner} does not have an native language to choose from (must have at least two for XenosocializedTrait!");
            return;
        }
        // Byrd end

        var nativeLanguages = prototypeLanguages.FindAll(it => it != SharedLanguageSystem.FallbackLanguagePrototype);
        if (nativeLanguages.Count == 0)
        {
            Log.Warning($"Entity {entity.Owner} does not have an native language to choose from (must have at least one non-GC for XenosocializedTrait!");
            return;
        }

        foreach (var language in nativeLanguages)
            _languages.RemoveLanguage(entity.Owner, language, true, true);
    }
}
// Derived from ForeignerTraitSystem.cs

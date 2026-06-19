using Content.Goobstation.Shared.Changeling.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Flash.Components;

namespace Content.Goobstation.Server.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private ISawmill _sawmill = default!;

    // Keep a list of all components changeling abilities we want to copy
    private static readonly Type[] ComponentsToCopy =
    {
        typeof(FlashImmunityComponent),
        typeof(EyeProtectionComponent),
        typeof(Shared.Overlays.NightVisionComponent),
        typeof(Shared.Overlays.ThermalVisionComponent),
        typeof(VoidAdaptionComponent),
    };

    /// <summary>
    /// Copies essential changeling ability components from one entity to another during polymorph.
    /// This ensures that changelings retain their special abilities (like thermal vision and space immunity)
    /// when transforming between different forms.
    /// </summary>
    /// <param name="uid">Source entity to copy components from</param>
    /// <param name="newEnt">Target entity to copy components to</param>
    /// <param name="comp">Optional ChangelingIdentityComponent reference</param>
    private void TryChangelingCopyComp(EntityUid uid, EntityUid newEnt, ChangelingIdentityComponent? comp)
    {
        // Try to copy each component individually
        foreach (var componentType in ComponentsToCopy)
        {
            try
            {
                if (!_entityManager.TryGetComponent(uid, componentType, out var component))
                    continue;

                var newComp = (Component) _serialization.CreateCopy(component, notNullableOverride: true);
                _entityManager.AddComponent(newEnt, newComp);
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Changling Polymorph Copy System: Failed to copy component {componentType.Name}: {ex.Message}");
            }
        }
    }
}

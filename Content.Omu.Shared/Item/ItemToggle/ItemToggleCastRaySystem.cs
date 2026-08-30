using Content.Omu.Shared.Item.ItemToggle.Components;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Light;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Omu.Shared.Item.ItemToggle;

/// <summary>
///     Causes items to cast a ray when toggled. The specifications of the ray are determined by ItemToggleCastRayComponent.
/// </summary>
public sealed class ItemToggleCastRaySystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemToggleCastRayComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeLocalEvent<ItemToggleCastRayComponent, LightToggleEvent>(OnLightToggled);
    }

    /// <summary>
    ///     Casts a ray when the item is toggled. Raises an event on all intersected entities which have the components specified by the ItemToggleCastRayComponent.
    /// </summary>
    private void OnItemToggled(Entity<ItemToggleCastRayComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated)
            return;

        var ray = new CollisionRay(_xformSystem.GetWorldPosition(ent.Owner), (_xformSystem.GetWorldRotation(ent.Owner) + Angle.FromDegrees(ent.Comp.RayOffsetDegrees)).ToVec(), 7); // collision mask is "in range obstructed".
        var rayCastResults = _physicsSystem.IntersectRay(_xformSystem.GetMapId(ent.Owner), ray, ent.Comp.RayLength, null, false);

        // For each entity which the ray intersected,
        foreach (var result in rayCastResults)
            // For each of the components we're looking for,
            foreach (var (_, component) in ent.Comp.RaiseEventAt)
                // If the intersected entity contains any one of those components:
                if (HasComp(result.HitEntity, component.Component.GetType()))
                {
                    // Direct an event to the intersected entity.
                    var ev = new ItemToggleRayHitEvent();
                    RaiseLocalEvent(result.HitEntity, ref ev);
                    break;
                }
    }

    /// <remarks>
    ///     2025/09/28
    ///     Bit of a stupid workaround here. This whole system came about because I wanted to apply the flashed visual effect to players with
    ///     photophobia, if somebody turned on a light in their face. But flashlights don't use the ItemToggleComponent, they use a HandheldLightComponent.
    ///     Why? I wish I knew!
    /// </remarks>
    private void OnLightToggled(Entity<ItemToggleCastRayComponent> ent, ref LightToggleEvent args)
    {
        // create an item toggle event, and give it as much data as we know.
        var itemToggleArgs = new ItemToggledEvent(true, args.IsOn, null);

        // call the method that SHOULD be called when a light is toggled.
        OnItemToggled(ent, ref itemToggleArgs);
    }

}

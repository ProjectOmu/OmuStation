using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Item.ItemToggle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ItemToggleCastRayComponent : Component
{
    /// <summary>
    ///     Entities with the specified component types will recieve an ItemToggleRayHitEvent if they intersect with the ray casted by this entity.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry RaiseEventAt;

    /// <summary>
    ///     Maximum length of the ray, in meters.
    /// </summary>
    [DataField]
    public float RayLength = 4;

    /// <summary>
    ///     Typically, the ray should point in the direction which the item is facing. If it isn't, then adjust this.
    /// </summary>
    [DataField]
    public double RayOffsetDegrees = -90;
}

/// <summary>
///     Raised directed on an entity when it is in range of a ray casted by the ItemToggleCastRaySystem, and has a component specified by ItemToggleCastRayComponent.
/// </summary>
[ByRefEvent]
public readonly record struct ItemToggleRayHitEvent()
{

}

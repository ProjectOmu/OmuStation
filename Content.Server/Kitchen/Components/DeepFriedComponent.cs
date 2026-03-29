using Content.Shared.Kitchen.Components;

namespace Content.Server.Kitchen.Components;

[RegisterComponent]
public sealed partial class DeepFriedComponent : SharedDeepFriedComponent
{
    /// <summary>
    /// What is the item's base price multiplied by?
    /// </summary>
    public float PriceCoefficient { get; set; } = 1.0f;

    /// <summary>
    /// What was the entity's original name before any modification?
    /// </summary>
    public string? OriginalName { get; set; }
}

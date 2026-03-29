using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen.Components;

/// <summary>
/// This is used for marking an entity as having been fried in a DeepFryer
/// </summary>
[NetworkedComponent]
public abstract partial class SharedDeepFriedComponent : Component
{

    /// <summary>
    /// How deep-fried is this item?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public int Crispiness { get; set; }
}

[Serializable, NetSerializable]
public enum DeepFriedVisuals : byte
{
    Fried,
}

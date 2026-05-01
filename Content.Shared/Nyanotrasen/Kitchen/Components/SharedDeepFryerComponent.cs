using Robust.Shared.Serialization;

namespace Content.Shared.Nyanotrasen.Kitchen.Components;

/// <summary>
/// This is used for frying things
/// </summary>
public abstract partial class SharedDeepFryerComponent : Component;

[Serializable, NetSerializable]
public enum DeepFryerVisuals : byte
{
    Bubbling,
}


/// <summary>
/// Contains network state for SharedDeepFryerComponent.
/// </summary>
[Serializable, NetSerializable]
public sealed class SharedDeepFryerState : ComponentState
{
    public SharedDeepFryerState(SharedDeepFryerComponent component)
    {

    }
}

using Robust.Shared.GameStates;
namespace Content.Omu.Shared.RadiusBuff.Components;

/// <summary>
/// Marks entities that should receive a buff from <see cref="RadiusBuffComponent"/>
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RadiusBuffReceiverComponent : Component;

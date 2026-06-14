using Robust.Shared.GameStates;
namespace Content.Shared._Omu.Components;

/// <summary>
/// Marker component for headwear that shouldn't fit plasmamen
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlasmaUnfitComponent : Component;
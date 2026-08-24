using Robust.Shared.GameStates;
namespace Content.Shared._Omu.Components;

/// <summary>
/// Used to mark CC's station beacons so that they are not valid targets for things like Collosus.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CentCommBeaconComponent : Component;

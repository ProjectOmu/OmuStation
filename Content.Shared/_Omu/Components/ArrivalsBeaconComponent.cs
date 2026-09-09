using Robust.Shared.GameStates;
namespace Content.Shared._Omu.Components;

/// <summary>
/// Used to mark Arrivals station beacons / warp points so that they are not valid targets for things like Collosus.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ArrivalsBeaconComponent : Component;

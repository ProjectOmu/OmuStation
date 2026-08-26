// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Content.Shared._NF.Shuttles.Events;
using System.Numerics;
using Content.Shared.Shuttles.Components;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class NavInterfaceState
{
    public float MaxRange;

    /// <summary>
    /// The relevant coordinates to base the radar around.
    /// </summary>
    public NetCoordinates? Coordinates;

    /// <summary>
    /// The relevant rotation to rotate the angle around.
    /// </summary>
    public Angle? Angle;

    public Dictionary<NetEntity, List<DockingPortState>> Docks;

    public bool RotateWithEntity = true;

    // _Starlight - transient laser beam traces for hitscan shuttle guns (e.g. Apollo)
    /// <summary>
    /// Transient laser beam traces to draw on radar (for hitscan weapons such as the Apollo).
    /// Each entry represents a fired beam; entries are expired server-side after a short duration.
    /// </summary>
    public List<RadarLaserData> Lasers = new(); // _Starlight

    // Frontier fields

    /// <summary>
    /// Custom display names for network port buttons.
    /// Key is the port ID, value is the display name.
    /// </summary>
    public Dictionary<string, string> NetworkPortNames;

    /// <summary>
    /// Frontier - the state of the shuttle's inertial dampeners
    /// </summary>
    public InertiaDampeningMode DampeningMode;

    /// <summary>
    /// Frontier: settable maximum IFF range
    /// </summary>
    public float? MaxIffRange = null;

    /// <summary>
    /// Frontier: settable coordinate visibility
    /// </summary>
    public bool HideCoords = false;

    // End Frontier fields
    public NavInterfaceState(
        float maxRange,
        NetCoordinates? coordinates,
        Angle? angle,
        Dictionary<NetEntity, List<DockingPortState>> docks,
        InertiaDampeningMode dampeningMode, // Frontier
        Dictionary<string, string>? networkPortNames = null)
    {
        MaxRange = maxRange;
        Coordinates = coordinates;
        Angle = angle;
        Docks = docks;
        DampeningMode = dampeningMode; // Frontier
        NetworkPortNames = networkPortNames ?? new Dictionary<string, string>();
    }
}

// _Starlight
/// <summary>
/// A transient laser beam drawn as a line on radar.
/// Origin is in entity-relative coordinates; the endpoint is origin + Direction * Length (in map space).
/// </summary>
[Serializable, NetSerializable]
public readonly struct RadarLaserData
{
    /// <summary>Entity-relative coordinates of the beam origin (the firing gun's position).</summary>
    public readonly NetCoordinates Origin;

    /// <summary>Normalized fire direction in map/world space.</summary>
    public readonly Vector2 Direction;

    /// <summary>Beam length in world units.</summary>
    public readonly float Length;

    /// <summary>Color of the laser line.</summary>
    public readonly Color Color;

    public RadarLaserData(NetCoordinates origin, Vector2 direction, float length, Color color)
    {
        Origin = origin;
        Direction = direction;
        Length = length;
        Color = color;
    }
}

[Serializable, NetSerializable]
public enum RadarConsoleUiKey : byte
{
    Key
}

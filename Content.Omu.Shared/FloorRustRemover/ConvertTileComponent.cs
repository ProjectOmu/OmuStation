// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.FloorRustRemover;

/// <summary>
/// This component is for items that can convert tiles to another tile via a doafter.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConvertTileComponent : Component
{
    /// <summary>
    /// How long it takes to convert the tile to the new one
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Delay = 8.0f;

    /// <summary>
    /// What tile ids to convert to what other tiles ids
    /// </summary>
    [DataField]
    public Dictionary<string, string> TileReactions = new();
}

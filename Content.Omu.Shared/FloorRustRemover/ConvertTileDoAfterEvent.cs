// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.FloorRustRemover;

[Serializable, NetSerializable]
public sealed partial class ConvertTileDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity GridNetUid;

    [DataField]
    public Vector2i TileIndices;

    [DataField]
    public string NewTileId;

    public ConvertTileDoAfterEvent(NetEntity gridNetUid, Vector2i tileIndices, string newTileId)
    {
        GridNetUid = gridNetUid;
        TileIndices = tileIndices;
        NewTileId = newTileId;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}

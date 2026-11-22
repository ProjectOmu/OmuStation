// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Paper;

[Serializable, NetSerializable]
public enum SignatureFontUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SignatureFontBoundUserInterfaceState(string name, string ownFont, string? font)
    : BoundUserInterfaceState
{
    public readonly string Name = name;
    public readonly string OwnFont = ownFont;
    public readonly string? Font = font;
}

[Serializable, NetSerializable]
public sealed class SignatureFontSelectMessage(string? font) : BoundUserInterfaceMessage
{
    public readonly string? Font = font;
}

// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Arcade;

[Serializable, NetSerializable]
public enum PolybiusArcadeUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PolybiusArcadePlayMessage : BoundUserInterfaceMessage;

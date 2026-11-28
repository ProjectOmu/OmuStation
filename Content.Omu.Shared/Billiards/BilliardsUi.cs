// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Billiards;

[Serializable, NetSerializable]
public enum BilliardsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum BilliardsMode : byte
{
    Unselected,
    Freeplay,
    Casual,
    SolidsStripes,
    Killer,
    FewestShots,
}

[DataRecord, Serializable, NetSerializable]
public partial record struct BilliardsBall(Vector2 Position, bool Pocketed);

[Serializable, NetSerializable]
public readonly record struct BilliardsPlayer(NetEntity Entity, string Name, int Score);

[Serializable, NetSerializable]
public sealed class BilliardsAimMessage(Vector2 direction, float power) : BoundUserInterfaceMessage
{
    public readonly Vector2 Direction = direction;
    public readonly float Power = power;
}

[Serializable, NetSerializable]
public sealed class BilliardsShootMessage(Vector2 direction, float power) : BoundUserInterfaceMessage
{
    public readonly Vector2 Direction = direction;
    public readonly float Power = power;
}

[Serializable, NetSerializable]
public sealed class BilliardsPlaceCueMessage(Vector2 position) : BoundUserInterfaceMessage
{
    public readonly Vector2 Position = position;
}

[Serializable, NetSerializable]
public sealed class BilliardsNewGameMessage(BilliardsMode mode) : BoundUserInterfaceMessage
{
    public readonly BilliardsMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class BilliardsJoinMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class BilliardsLeaveMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class BilliardsSkipMessage : BoundUserInterfaceMessage;

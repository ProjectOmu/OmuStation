// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Omu.Preferences;

[Serializable, NetSerializable]
public sealed class CharacterQueueState(
    Dictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities,
    HashSet<int> activeSlots,
    List<int> order)
{
    public readonly Dictionary<ProtoId<JobPrototype>, JobPriority> JobPriorities = jobPriorities;
    public readonly HashSet<int> ActiveSlots = activeSlots;
    public readonly List<int> Order = order;
}

[Serializable, NetSerializable]
public sealed class CharacterQueueStateEvent(CharacterQueueState state) : EntityEventArgs
{
    public readonly CharacterQueueState State = state;
}

[Serializable, NetSerializable]
public sealed class SetCharacterQueueEvent(CharacterQueueState state, uint sequence) : EntityEventArgs
{
    public readonly CharacterQueueState State = state;
    public readonly uint Sequence = sequence;
}

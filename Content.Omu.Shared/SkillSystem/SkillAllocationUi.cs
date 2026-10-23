// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Omu.Shared.SkillSystem;

[Serializable, NetSerializable]
public enum SkillAllocationUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SkillAllocationBoundUserInterfaceState(List<SkillAllocationEntry> skills, int points)
    : BoundUserInterfaceState
{
    public readonly List<SkillAllocationEntry> Skills = skills;
    public readonly int Points = points;
}

[Serializable, NetSerializable]
public sealed class SkillAllocationEntry(string id, string name, string? description, int level)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string? Description = description;
    public readonly int Level = level;
}

[Serializable, NetSerializable]
public sealed class SkillAllocationConfirmMessage(Dictionary<string, int> allocation) : BoundUserInterfaceMessage
{
    public readonly Dictionary<string, int> Allocation = allocation;
}

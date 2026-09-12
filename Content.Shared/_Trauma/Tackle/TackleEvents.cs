// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;

namespace Content.Shared._Trauma.Tackle;

[ByRefEvent]
public record struct TackleEvent(
    float Range,
    float Speed,
    float StaminaCost,
    TimeSpan KnockdownTime,
    EntityUid User,
    EntityUid? Source = null) : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.GLOVES;
}

[ByRefEvent]
public record struct CalculateTackleModifierEvent(float Modifier = 0f);

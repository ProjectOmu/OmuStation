// SPDX-License-Identifier: MIT

namespace Content.Omu.Common.DoAfter;

[ByRefEvent]
public record struct DoAfterUsedDelayEvent(EntityUid User, float Multiplier = 1f, bool Cancelled = false);

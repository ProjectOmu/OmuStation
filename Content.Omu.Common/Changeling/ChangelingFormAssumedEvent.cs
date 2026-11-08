// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Common.Changeling;

[ByRefEvent]
public readonly record struct ChangelingFormAssumedEvent(EntityUid Original);

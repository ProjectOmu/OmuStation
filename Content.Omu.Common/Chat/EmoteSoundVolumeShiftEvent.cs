// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Common.Chat;

[ByRefEvent]
public record struct EmoteSoundVolumeShiftEvent(string EmoteId, float Volume = 0f);

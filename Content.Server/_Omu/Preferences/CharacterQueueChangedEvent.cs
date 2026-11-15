// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Network;

namespace Content.Server._Omu.Preferences;

public readonly record struct CharacterQueueChangedEvent(NetUserId Player);

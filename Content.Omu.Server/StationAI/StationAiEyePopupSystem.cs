// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;

namespace Content.Omu.Server.StationAI;

public sealed class StationAiEyePopupSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;

    public EntityUid? GetEye(EntityUid uid)
    {
        return _stationAi.TryGetCore(uid, out var core) ? core.Comp?.RemoteEntity : null;
    }

    public void PopupToSelf(string message, EntityUid uid)
    {
        _popup.PopupEntity(message, GetEye(uid) ?? uid, uid, PopupType.Medium);
    }
}

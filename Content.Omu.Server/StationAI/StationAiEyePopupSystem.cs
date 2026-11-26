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
        if (HasComp<StationAiHeldComponent>(uid))
            _popup.PopupCursor(message, uid, PopupType.Medium);
        else
            _popup.PopupEntity(message, uid, uid, PopupType.Medium);
    }
}

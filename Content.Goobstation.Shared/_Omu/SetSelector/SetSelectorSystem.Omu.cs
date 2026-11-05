// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Goobstation.Shared.SetSelector;

public sealed partial class SetSelectorSystem
{
    [Dependency] private readonly ISharedPlaytimeManager _playtime = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private void OnUIClosed(Entity<SetSelectorComponent> selector, ref BoundUIClosedEvent args)
    {
        selector.Comp.SelectedSets.Clear();
        UpdateUI(selector.Owner, selector.Comp);
    }

    private bool CanSelect(Entity<SetSelectorComponent> selector, int index, EntityUid actor)
    {
        if (index < 0 || index >= selector.Comp.AvailableSets.Count)
            return false;

        _players.TryGetSessionByEntity(actor, out var session);
        var set = _proto.Index(selector.Comp.AvailableSets[index]);
        if (set.CanSelect(session, _playtime, EntityManager, _proto, out var reason))
            return true;

        _popup.PopupEntity(reason, selector.Owner, actor);
        return false;
    }
}

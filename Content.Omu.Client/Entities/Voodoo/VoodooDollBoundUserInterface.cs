// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Controls;
using Content.Omu.Shared.Entities.Voodoo;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Entities.Voodoo;

public sealed class VoodooDollBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not VoodooDollBoundUserInterfaceState voodooState)
            return;

        var options = new List<RadialMenuOptionBase>();
        foreach (var candidate in voodooState.Candidates)
        {
            options.Add(new RadialMenuActionOption<NetEntity>(ChooseVictim, candidate.Entity)
            {
                ToolTip = candidate.Name,
                IconSpecifier = EntMan.TryGetEntity(candidate.Entity, out var uid)
                    ? RadialMenuIconSpecifier.With(uid)
                    : RadialMenuIconSpecifier.With(candidate.Prototype),
            });
        }

        _menu.SetButtons(options);
    }

    private void ChooseVictim(NetEntity victim)
    {
        SendMessage(new VoodooDollChooseVictimMessage(victim));
    }
}

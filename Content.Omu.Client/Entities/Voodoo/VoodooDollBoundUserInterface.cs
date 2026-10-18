// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
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

        options.AddRange(
            voodooState.Candidates.Select(c => new RadialMenuActionOption<NetEntity>(ChooseVictim, c.Entity)
            {
                ToolTip = c.Name,
                IconSpecifier = EntMan.TryGetEntity(c.Entity, out var uid)
                    ? RadialMenuIconSpecifier.With(uid)
                    : RadialMenuIconSpecifier.With(c.Prototype),
            })
        );

        _menu.SetButtons(options);
    }

    private void ChooseVictim(NetEntity victim)
    {
        SendMessage(new VoodooDollChooseVictimMessage(victim));
    }
}

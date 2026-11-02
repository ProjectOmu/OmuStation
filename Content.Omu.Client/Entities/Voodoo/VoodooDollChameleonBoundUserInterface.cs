// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.Clothing.UI;
using Content.Omu.Shared.Entities.Voodoo;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Omu.Client.Entities.Voodoo;

public sealed class VoodooDollChameleonBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private ChameleonMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ChameleonMenu>();
        _menu.OnIdSelected += id => SendMessage(new VoodooDollChameleonSelectedMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null || state is not VoodooDollChameleonBoundUserInterfaceState chameleonState)
            return;

        var own = EntMan.GetComponent<MetaDataComponent>(Owner).EntityPrototype?.ID;
        var targets = EntMan.System<VoodooDollChameleonSystem>().GetValidTargets(chameleonState.Parent);
        _menu.UpdateState(own == null ? targets : targets.Prepend(new EntProtoId(own)),
            chameleonState.Selected?.Id ?? own);
    }
}

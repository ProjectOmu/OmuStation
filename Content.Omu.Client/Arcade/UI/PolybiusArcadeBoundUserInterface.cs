// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Arcade;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Arcade.UI;

public sealed class PolybiusArcadeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    protected override void Open()
    {
        base.Open();

        var window = this.CreateWindow<PolybiusArcadeWindow>();
        window.OnScreenDied += () => SendMessage(new PolybiusArcadePlayMessage());
    }
}

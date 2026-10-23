// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.SkillSystem;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.SkillSystem.UI;

public sealed class SkillAllocationBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SkillAllocationWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SkillAllocationWindow>();
        _window.OnConfirmed += allocation => SendMessage(new SkillAllocationConfirmMessage(allocation));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SkillAllocationBoundUserInterfaceState current)
            _window?.UpdateState(current);
    }
}

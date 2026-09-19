// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Omu.Revs;
using Content.Shared.Speech.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Omu.Revs;

/// <summary>
/// Initializes a <see cref="MeleeSpeechWindow"/> and updates it when new server messages are received.
/// Carbon copy of the melee one for battlecry - but tweaked cause we don't need the melee speech
/// </summary>
public sealed class RevObjectiveBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private RevObjectiveWindow? _window;

    public RevObjectiveBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<RevObjectiveWindow>();
        _window.OnObjectiveEntered += OnObjectiveChanged;

        if (EntMan.TryGetComponent<BookConverterComponent>(Owner, out var component))
            _window.SetUiText(component.TitleBar, component.DescriptorBar);
    }

    private void OnObjectiveChanged(string newObjective)
    {
        SendMessage(new ObjectiveChangedMessage(newObjective));
    }

    /// <summary>
    /// Update the UI state based on server-sent info
    /// </summary>
    /// <param name="state"></param>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not RevObjectiveBoundUserInterfaceState cast)
            return;

        _window.SetCurrentObjective(cast.CurrentObjective);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
    }
}

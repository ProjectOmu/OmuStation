// SPDX-FileCopyrightText: 2024 MilenVolf <63782763+MilenVolf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 chromiumboy <50505512+chromiumboy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Milon <milonpl.git@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos.Components;
using Content.Shared.Shuttles.Events; // Frontier
using Content.Shared._NF.Atmos.BUI; // Frontier

namespace Content.Client.Atmos.Consoles;

public sealed class AtmosAlertsComputerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AtmosAlertsComputerWindow? _menu;

    public AtmosAlertsComputerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _menu = new AtmosAlertsComputerWindow(this, Owner);
        _menu.OpenCentered();
        _menu.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        var castState = (AtmosAlertsComputerBoundInterfaceState) state;

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
        _menu?.UpdateUI(xform?.Coordinates, castState.AirAlarms, castState.FireAlarms, castState.FocusData, castState.Gaslocks, castState.FocusGaslockData); // Frontier: add gaslocks, focusGaslockData
    }

    public void SendFocusChangeMessage(NetEntity? netEntity)
    {
        SendMessage(new AtmosAlertsComputerFocusChangeMessage(netEntity));
    }

    public void SendDeviceSilencedMessage(NetEntity netEntity, bool silenceDevice)
    {
        SendMessage(new AtmosAlertsComputerDeviceSilencedMessage(netEntity, silenceDevice));
    }

    // Frontier: gaslock message
    public void SendGaslockChangeDirectionMessage(NetEntity netEntity, bool direction)
    {
        SendMessage(new RemoteGasPressurePumpChangePumpDirectionMessage(netEntity, direction));
    }

    public void SendGaslockPressureChangeMessage(NetEntity netEntity, float pressure)
    {
        SendMessage(new RemoteGasPressurePumpChangeOutputPressureMessage(netEntity, pressure));
    }

    public void SendGaslockChangeEnabled(NetEntity netEntity, bool enabled)
    {
        SendMessage(new RemoteGasPressurePumpToggleStatusMessage(netEntity, enabled));
    }

    public void SendGaslockUndock(NetEntity netEntity)
    {
        SendMessage(new UndockRequestMessage { DockEntity = netEntity });
    }
    // End Frontier

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Dispose();
    }
}
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Overlays;
using Content.Goobstation.Client.Overlays;
using Content.Goobstation.Shared.Overlays;
using Content.Omu.Shared.Overlays;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Omu.Client.Overlays;

public sealed class JaniVisionSystem : EquipmentHudSystem<JaniVisionComponent>
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private JaniVisionOverlay _janiOverlay = default!;
    private BaseSwitchableOverlay<JaniVisionComponent> _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JaniVisionComponent, SwitchableOverlayToggledEvent>(OnToggle);

        _janiOverlay = new JaniVisionOverlay();
        _overlay = new BaseSwitchableOverlay<JaniVisionComponent>
        {
            RestrictToPlayerViewport = true
        };
    }

    protected override void OnRefreshComponentHud(Entity<JaniVisionComponent> ent,
        ref RefreshEquipmentHudEvent<JaniVisionComponent> args)
    {
        if (!ent.Comp.IsEquipment)
            base.OnRefreshComponentHud(ent, ref args);
    }

    protected override void OnRefreshEquipmentHud(Entity<JaniVisionComponent> ent,
        ref InventoryRelayedEvent<RefreshEquipmentHudEvent<JaniVisionComponent>> args)
    {
        if (ent.Comp.IsEquipment)
            base.OnRefreshEquipmentHud(ent, ref args);
    }

    private void OnToggle(Entity<JaniVisionComponent> ent, ref SwitchableOverlayToggledEvent args)
    {
        RefreshOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<JaniVisionComponent> args)
    {
        base.UpdateInternal(args);
        JaniVisionComponent? jvComp = null;
        foreach (var comp in args.Components)
        {
            if (!comp.IsActive && (comp.PulseTime <= 0f || comp.PulseAccumulator >= comp.PulseTime))
                continue;

            if (jvComp == null)
                jvComp = comp;
            else if (!jvComp.DrawOverlay && comp.DrawOverlay)
                jvComp = comp;
            else if (jvComp.DrawOverlay == comp.DrawOverlay && jvComp.PulseTime > 0f && comp.PulseTime <= 0f)
                jvComp = comp;

        }

        UpdateJaniOverlay(jvComp);
        UpdateOverlay(jvComp);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        UpdateOverlay(null);
        UpdateJaniOverlay(null);
    }

    private void UpdateJaniOverlay(JaniVisionComponent? comp)
    {
        _janiOverlay.Comp = comp;

        switch (comp)
        {
            case not null when !_overlayMan.HasOverlay<JaniVisionOverlay>():
                _overlayMan.AddOverlay(_janiOverlay);
                break;
            case null:
                _overlayMan.RemoveOverlay(_janiOverlay);
                break;
        }
    }

    private void UpdateOverlay(JaniVisionComponent? jvComp)
    {
        _overlay.Comp = jvComp;

        switch (jvComp)
        {
            case { DrawOverlay: true } when !_overlayMan.HasOverlay<BaseSwitchableOverlay<JaniVisionComponent>>():
                _overlayMan.AddOverlay(_overlay);
                break;
            case null or { DrawOverlay: false }:
                _overlayMan.RemoveOverlay(_overlay);
                break;
        }

        // Night vision overlay is prioritized
        _overlay.IsActive = !_overlayMan.HasOverlay<BaseSwitchableOverlay<NightVisionComponent>>();
    }
}

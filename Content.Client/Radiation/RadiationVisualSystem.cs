// SPDX-FileCopyrightText: 2026 puntsss <bex.ish.aholic@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radiation.Events;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.Radiation;

public sealed class RadiationVisualSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private RadiationVisualOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new RadiationVisualOverlay(_timing);
        _overlayManager.AddOverlay(_overlay);

        SubscribeNetworkEvent<RadiationVisualsEvent>(OnRadiationVisuals);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnRadiationVisuals(RadiationVisualsEvent ev)
    {
        _overlay?.Show(ev.Intensity, TimeSpan.FromSeconds(ev.Duration));
    }
}

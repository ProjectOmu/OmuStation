using System.Numerics;
using Content.Shared._DV.CCVars;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;

namespace Content.Client._DV.RoundEnd;

public sealed class NoEorgPopupSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private NoEorgPopup? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RoundEndMessageEvent>(OnRoundEnd);
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        if (_cfg.GetCVar(DCCVars.SkipRoundEndNoEorgPopup) || _cfg.GetCVar(DCCVars.RoundEndNoEorgPopup) == false)
            return;

        OpenNoEorgPopup();
    }

    private void OpenNoEorgPopup()
    {
        if (_window != null)
            return;

        _window = new NoEorgPopup();

        // Omu Edit
        // Open the window positioned on the left side, centered vertically
        // First parameter is the relative position (0 = left, 1 = right)
        // Second parameter is the vertical position (0.5 = center)
        _window.OpenCenteredAt(new Vector2(0f, 0.5f));

        _window.OnClose += () =>
        {
            _window = null;
        };
    }
}

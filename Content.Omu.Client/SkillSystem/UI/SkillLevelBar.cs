// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.UserInterface.Systems;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Omu.Client.SkillSystem.UI;

public sealed class SkillLevelBar : Control
{
    public const string StylePropertyTrack = "skill-bar-track";
    public const string StylePropertyBorder = "skill-bar-border";

    private const int MaxLevel = 6;
    private const float PendingAlpha = 0.5f;
    private const float GradientStep = 4f;
    private const float GreenHue = 1f / 3f;

    private readonly IConfigurationManager _cfg;
    private readonly ProgressColorSystem _progressColor;

    private DrawVertexUV2DColor[] _vertices = [];
    private int _trained;
    private int _pending;

    public SkillLevelBar()
    {
        _cfg = IoCManager.Resolve<IConfigurationManager>();
        _progressColor = IoCManager.Resolve<IEntityManager>().System<ProgressColorSystem>();
        MinSize = new Vector2(120, 14);
        MouseFilter = MouseFilterMode.Pass;
        VerticalAlignment = VAlignment.Center;
    }

    public void SetLevels(int trained, int pending)
    {
        _trained = trained;
        _pending = pending;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
        TryGetStyleProperty<Color>(StylePropertyTrack, out var track);
        TryGetStyleProperty<Color>(StylePropertyBorder, out var border);
        handle.DrawRect(box, track);

        var effective = Math.Clamp(_trained + _pending, 0, MaxLevel);
        var trainedWidth = box.Width * Math.Clamp(_trained, 0, MaxLevel) / MaxLevel;
        var effectiveWidth = box.Width * effective / MaxLevel;
        DrawGradient(handle, 0, trainedWidth, box, 1f);
        DrawGradient(handle, trainedWidth, effectiveWidth, box, PendingAlpha);

        var outline = _trained + _pending < 0 ? LevelColor(0) : border;
        handle.DrawRect(box, outline, false);
    }

    private void DrawGradient(DrawingHandleScreen handle, float from, float to, UIBox2 box, float alpha)
    {
        if (to <= from)
            return;

        var steps = Math.Max(1, (int) MathF.Ceiling((to - from) / (GradientStep * UIScale)));
        var count = (steps + 1) * 2;
        if (_vertices.Length < count)
            _vertices = new DrawVertexUV2DColor[count];

        for (var i = 0; i <= steps; i++)
        {
            var x = from + (to - from) * i / steps;
            var color = Color.FromSrgb(LevelColor(x / box.Width).WithAlpha(alpha));
            _vertices[i * 2] = new DrawVertexUV2DColor(new Vector2(x, 0), color);
            _vertices[i * 2 + 1] = new DrawVertexUV2DColor(new Vector2(x, box.Height), color);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, _vertices.AsSpan(0, count));
    }

    private Color LevelColor(float fraction)
    {
        if (_cfg.GetCVar(CCVars.AccessibilityColorblindFriendly))
            return _progressColor.GetProgressColor(fraction);

        return Color.FromHsv(new Vector4(fraction * GreenHue, 1f, 1f, 1f));
    }
}

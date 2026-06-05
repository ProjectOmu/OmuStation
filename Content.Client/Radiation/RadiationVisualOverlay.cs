using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
// SPDX-FileCopyrightText: 2026 puntsss <bex.ish.aholic@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client.Radiation;

public sealed class RadiationVisualOverlay : Overlay
{
    private readonly IGameTiming _timing;

    private float _currentIntensity;
    private float _targetIntensity;
    private TimeSpan _endTime;
    private TimeSpan _lastDrawTime;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public RadiationVisualOverlay(IGameTiming timing)
    {
        _timing = timing;
    }

    public void Show(float intensity, TimeSpan duration)
    {
        _targetIntensity = Math.Clamp(intensity, 0f, 1f);
        _endTime = _timing.CurTime + duration;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.OmuRadiationVisualsEnabled))
            return;

        var deltaSeconds = _lastDrawTime == default
            ? 0f
            : Math.Clamp((float) (_timing.CurTime - _lastDrawTime).TotalSeconds, 0f, 0.1f);

        _lastDrawTime = _timing.CurTime;

        var wantedIntensity = _timing.CurTime < _endTime ? _targetIntensity : 0f;
        var speed = wantedIntensity > _currentIntensity ? 18f : 2.5f;
        _currentIntensity = MathHelper.Lerp(_currentIntensity, wantedIntensity, Math.Clamp(deltaSeconds * speed, 0f, 1f));

        if (_currentIntensity <= 0.005f)
            return;

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var width = bounds.Width;
        var height = bounds.Height;
        var intensity = _currentIntensity;

        var seed = (int) (_timing.CurTime.TotalMilliseconds / 8);
        var random = new Random(seed);

        var dimAlpha = Math.Clamp(0.004f + 0.020f * intensity, 0.004f, 0.030f);
        handle.DrawRect(bounds, new Color(0f, 0f, 0f, dimAlpha));

        var deadPixels = (int) (65 + 250 * intensity);
        for (var i = 0; i < deadPixels; i++)
        {
            var x = bounds.Left + (float) random.NextDouble() * width;
            var y = bounds.Top + (float) random.NextDouble() * height;

            float w;
            float h;
            var r = random.NextDouble();
            if (r < 0.80)
            {
                w = 1f + (float) random.NextDouble() * (1.3f + 0.8f * intensity);
                h = 1f + (float) random.NextDouble() * (1.3f + 0.8f * intensity);
            }
            else if (r < 0.95)
            {
                w = 1.5f + (float) random.NextDouble() * (2.0f + 1.3f * intensity);
                h = 1.5f + (float) random.NextDouble() * (2.0f + 1.3f * intensity);
            }
            else
            {
                w = 3f + (float) random.NextDouble() * (4f + 3f * intensity);
                h = 1.5f + (float) random.NextDouble() * (2.5f + 2f * intensity);
            }

            var alpha = 0.18f + 0.40f * intensity * (0.45f + (float) random.NextDouble() * 0.75f);
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(w, h)),
                new Color(0f, 0f, 0f, Math.Clamp(alpha, 0f, 0.70f)));
        }

        var hotPixels = (int) (24 + 120 * intensity);
        for (var i = 0; i < hotPixels; i++)
        {
            var x = bounds.Left + (float) random.NextDouble() * width;
            var y = bounds.Top + (float) random.NextDouble() * height;
            var roll = random.NextDouble();

            float w;
            float h;
            if (roll < 0.84)
            {
                w = 0.8f + (float) random.NextDouble() * (1.1f + 1.1f * intensity);
                h = w;
            }
            else
            {
                w = 2f + (float) random.NextDouble() * (4f + 8f * intensity);
                h = 1f + (float) random.NextDouble() * 1.1f;
            }

            var alpha = 0.14f + 0.28f * intensity * (0.40f + (float) random.NextDouble() * 0.80f);
            var shade = 0.84f + (float) random.NextDouble() * 0.16f;
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(w, h)),
                new Color(shade, shade, shade, Math.Clamp(alpha, 0f, 0.44f)));
        }

        // More noticeable blueish-grey sensor specks.
        var blueGreyDots = (int) (18 + 80 * intensity);
        for (var i = 0; i < blueGreyDots; i++)
        {
            var x = bounds.Left + (float) random.NextDouble() * width;
            var y = bounds.Top + (float) random.NextDouble() * height;
            var size = 1f + (float) random.NextDouble() * (1.8f + 1.7f * intensity);
            var alpha = 0.16f + 0.24f * intensity * (0.45f + (float) random.NextDouble() * 0.80f);
            var color = new Color(0.60f, 0.68f, 0.80f, Math.Clamp(alpha, 0f, 0.34f));
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(size, size)), color);
        }

        var burstCount = (int) (4 + 16 * intensity);
        for (var i = 0; i < burstCount; i++)
        {
            var centerX = bounds.Left + (float) random.NextDouble() * width;
            var centerY = bounds.Top + (float) random.NextDouble() * height;
            var radius = 5f + (float) random.NextDouble() * (10f + 18f * intensity);
            var dots = random.Next(5, 12);

            for (var j = 0; j < dots; j++)
            {
                var x = centerX + ((float) random.NextDouble() - 0.5f) * radius;
                var y = centerY + ((float) random.NextDouble() - 0.5f) * radius;
                var size = 1f + (float) random.NextDouble() * (1.6f + 1.8f * intensity);
                var alpha = 0.16f + 0.32f * intensity;
                var roll = random.NextDouble();

                Color color;
                if (roll < 0.52)
                    color = new Color(0f, 0f, 0f, alpha);
                else if (roll < 0.78)
                    color = new Color(1f, 1f, 1f, alpha * 0.82f);
                else
                    color = new Color(0.60f, 0.68f, 0.80f, alpha * 0.72f);

                handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(size, size)), color);
            }
        }

        var streakCount = (int) (3 + 10 * intensity);
        for (var i = 0; i < streakCount; i++)
        {
            var horizontal = random.NextDouble() < 0.72;
            var bright = random.NextDouble() < 0.55;
            var shade = bright ? 1f : 0f;
            var baseAlpha = 0.08f + 0.18f * intensity;

            if (horizontal)
            {
                var x = bounds.Left + (float) random.NextDouble() * width;
                var y = bounds.Top + (float) random.NextDouble() * height;
                var w = 4f + (float) random.NextDouble() * (10f + 18f * intensity);
                var h = 1f + (float) random.NextDouble() * 0.9f;
                handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(w, h)),
                    new Color(shade, shade, shade, baseAlpha));
                handle.DrawRect(UIBox2.FromDimensions(new Vector2(x + w, y), new Vector2(w * 0.6f, h)),
                    new Color(shade, shade, shade, baseAlpha * 0.35f));
            }
            else
            {
                var x = bounds.Left + (float) random.NextDouble() * width;
                var y = bounds.Top + (float) random.NextDouble() * height;
                var w = 1f + (float) random.NextDouble() * 0.9f;
                var h = 4f + (float) random.NextDouble() * (10f + 18f * intensity);
                handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(w, h)),
                    new Color(shade, shade, shade, baseAlpha));
                handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y + h), new Vector2(w, h * 0.6f)),
                    new Color(shade, shade, shade, baseAlpha * 0.35f));
            }
        }

        if (intensity >= 0.24f)
        {
            var dropoutCount = (int) (1 + 5 * intensity);
            for (var i = 0; i < dropoutCount; i++)
            {
                var x = bounds.Left + (float) random.NextDouble() * width;
                var y = bounds.Top + (float) random.NextDouble() * height;
                var w = 5f + (float) random.NextDouble() * (12f + 22f * intensity);
                var h = 2f + (float) random.NextDouble() * (4f + 8f * intensity);
                var alpha = 0.04f + 0.08f * intensity;
                handle.DrawRect(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(w, h)),
                    new Color(0f, 0f, 0f, alpha));
            }
        }

        if (intensity >= 0.20f)
        {
            var flicker = (float) (Math.Sin(_timing.CurTime.TotalSeconds * 37f) + 1f) / 2f;
            var flickerAlpha = flicker * 0.022f * intensity;
            handle.DrawRect(bounds, new Color(0f, 0f, 0f, flickerAlpha));
        }
    }
}

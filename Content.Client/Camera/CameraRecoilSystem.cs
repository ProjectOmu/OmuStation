using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client.Camera;

public sealed class CameraRecoilSystem : SharedCameraRecoilSystem
{
    [Dependency] private readonly IConfigurationManager _configManager = default!;

    private float _intensity;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CameraKickEvent>(OnCameraKick);

        Subs.CVar(_configManager, CCVars.ScreenShakeIntensity, OnCvarChanged, true);
    }

    private void OnCvarChanged(float value)
    {
        _intensity = value;
    }

    private void OnCameraKick(CameraKickEvent ev)
    {
        KickCamera(GetEntity(ev.NetEntity), ev.Recoil);
    }

    public override void KickCamera(EntityUid uid, Vector2 recoil, CameraRecoilComponent? component = null)
    {
        if (_intensity == 0)
            return;

        if (!Resolve(uid, ref component, false))
            return;

        // Validate input recoil vector
        if (!float.IsFinite(recoil.X) || !float.IsFinite(recoil.Y))
            return;

        recoil *= _intensity;

        // Validate recoil after intensity multiplication
        if (!float.IsFinite(recoil.X) || !float.IsFinite(recoil.Y))
            return;

        // Reset CurrentKick if it contains invalid values
        if (!float.IsFinite(component.CurrentKick.X) || !float.IsFinite(component.CurrentKick.Y))
            component.CurrentKick = Vector2.Zero;

        // Use really bad math to "dampen" kicks when we're already kicked.
        var existing = component.CurrentKick.Length();
        if (!float.IsFinite(existing))
            existing = 0f;

        var dampen = existing / KickMagnitudeMax;
        if (!float.IsFinite(dampen))
            dampen = 0f;

        component.CurrentKick += recoil * (1 - dampen);

        // Validate after addition
        if (!float.IsFinite(component.CurrentKick.X) || !float.IsFinite(component.CurrentKick.Y))
        {
            component.CurrentKick = Vector2.Zero;
            return;
        }

        var currentLength = component.CurrentKick.Length();
        if (currentLength > KickMagnitudeMax && float.IsFinite(currentLength))
        {
            var normalized = component.CurrentKick.Normalized();
            // Only use normalized if it's valid
            if (float.IsFinite(normalized.X) && float.IsFinite(normalized.Y))
            {
                component.CurrentKick = normalized * KickMagnitudeMax;
            }
            else
            {
                component.CurrentKick = Vector2.Zero;
            }
        }

        component.LastKickTime = 0;
    }
}
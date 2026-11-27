// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Arcade;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Omu.Client.Arcade;

public sealed class PolybiusLightFlickerSystem : EntitySystem
{
    private const double FlickerSlotSeconds = 0.07;

    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        using var query = EntityQueryEnumerator<PolybiusLightFlickerComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var flicker, out var light))
        {
            if (flicker.EndTime > _timing.CurTime)
            {
                flicker.LightBefore ??= light.Enabled;

                var slot = (long) (_timing.CurTime.TotalSeconds / FlickerSlotSeconds);
                var random = new System.Random(HashCode.Combine(GetNetEntity(uid).Id, slot));
                _light.SetEnabled(uid, random.Next(2) == 0, light);
                continue;
            }

            if (flicker.LightBefore is not { } before)
                continue;

            _light.SetEnabled(uid, before, light);
            flicker.LightBefore = null;
        }
    }
}

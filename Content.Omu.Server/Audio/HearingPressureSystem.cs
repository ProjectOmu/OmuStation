// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Audio;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Ghost;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Audio;

public sealed class HearingPressureSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float PressureStep = 5f;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        EnsureComp<HearingPressureComponent>(args.Entity);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        RemComp<HearingPressureComponent>(args.Entity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<HearingPressureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hearing, out var xform))
        {
            var pressure = HasComp<GhostComponent>(uid)
                ? Atmospherics.OneAtmosphere
                : _atmosphere.GetContainingMixture((uid, xform))?.Pressure ?? 0f;

            pressure = MathF.Min(MathF.Round(pressure / PressureStep) * PressureStep, Atmospherics.OneAtmosphere);

            if (hearing.Pressure.Equals(pressure))
                continue;

            hearing.Pressure = pressure;
            Dirty(uid, hearing);
        }
    }
}

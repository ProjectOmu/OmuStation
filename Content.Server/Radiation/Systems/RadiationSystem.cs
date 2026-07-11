// SPDX-FileCopyrightText: 2022 Alex Evgrashin <aevgrashin@yandex.ru>
// SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Thomas <87614336+Aeshus@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 puntsss <bex.ish.aholic@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Radiation.Components;
using Content.Shared.Damage;
using Robust.Shared.Player;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Content.Shared.Radiation.Events;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Radiation.Systems;

public sealed partial class RadiationSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<RadiationBlockingContainerComponent> _blockerQuery;
    private EntityQuery<RadiationGridResistanceComponent> _resistanceQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<StackComponent> _stackQuery;

    private float _accumulator;
    private List<SourceData> _sources = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeCvars();
        InitRadBlocking();

        _blockerQuery = GetEntityQuery<RadiationBlockingContainerComponent>();
        _resistanceQuery = GetEntityQuery<RadiationGridResistanceComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < GridcastUpdateRate)
            return;

        UpdateGridcast();
        UpdateResistanceDebugOverlay();
        _accumulator = 0f;
    }

    public void IrradiateEntity(EntityUid uid, float radsPerSecond, float time)
    {
        var msg = new OnIrradiatedEvent(time, radsPerSecond, uid);
        RaiseLocalEvent(uid, msg);

        TrySendRadiationVisuals(uid, radsPerSecond);
        TryWarnRadiationHealth(uid, radsPerSecond);
    }

    private void TryWarnRadiationHealth(EntityUid uid, float radsPerSecond)
    {
        if (radsPerSecond <= 0f)
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var totalDamage = damageable.TotalDamage.Float();

        var stage = totalDamage switch
        {
            >= 90f => 5,
            >= 70f => 4,
            >= 45f => 3,
            >= 25f => 2,
            >= 10f => 1,
            _ => 0
        };

        if (stage == 0)
            return;

        var warning = EnsureComp<RadiationWarningComponent>(uid);
        var worsened = stage > warning.LastWarningStage;
        if (!worsened && _timing.CurTime < warning.NextWarningTime)
            return;

        var message = stage switch
        {
            1 => "You feel uneasy.",
            2 => "Your skin prickles faintly.",
            3 => "Your stomach twists slightly.",
            4 => "You feel lightheaded.",
            5 => "A sudden wave of nausea hits you.",
            _ => null
        };

        if (message == null)
            return;

        var cooldown = stage switch
        {
            >= 5 => TimeSpan.FromSeconds(15),
            >= 4 => TimeSpan.FromSeconds(20),
            _ => TimeSpan.FromSeconds(35)
        };

        warning.LastWarningStage = Math.Max(warning.LastWarningStage, stage);
        warning.NextWarningTime = _timing.CurTime + cooldown;

        _popup.PopupEntity(message, uid, uid);
    }

    private void TrySendRadiationVisuals(EntityUid uid, float radsPerSecond)
    {
        // Do not start the visual effect until about 1 rad/sec.
        if (radsPerSecond < 1f)
            return;

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        // Scale off the CURRENT radiation being received.
        // 1 rad/sec = subtle start, 6+ rads/sec = strong camera damage effect.
        var normalized = Math.Clamp((radsPerSecond - 1f) / 5f, 0f, 1f);
        var intensity = Math.Clamp(0.12f + MathF.Sqrt(normalized) * 0.88f, 0.12f, 1f);

        RaiseNetworkEvent(new RadiationVisualsEvent(intensity, 3.25f), actor.PlayerSession);
    }

    public void SetSourceEnabled(Entity<RadiationSourceComponent?> entity, bool val)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        entity.Comp.Enabled = val;
    }

    public void SetCanReceive(EntityUid uid, bool canReceive)
    {
        if (canReceive)
            EnsureComp<RadiationReceiverComponent>(uid);
        else
            RemComp<RadiationReceiverComponent>(uid);
    }
}

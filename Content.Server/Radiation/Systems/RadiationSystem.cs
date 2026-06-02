// SPDX-FileCopyrightText: 2022 Alex Evgrashin <aevgrashin@yandex.ru>
// SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Thomas <87614336+Aeshus@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Radiation.Components;
using Content.Shared.Radiation.Components;
using Content.Shared.Radiation.Events;
using Content.Shared.Stacks;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

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

        TryWarnRadiationHealth(uid, radsPerSecond);
    }

    private void TryWarnRadiationHealth(EntityUid uid, float radsPerSecond)
    {
        // Only run this when the entity is actively receiving radiation.
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

        // If their condition gets worse, warn immediately.
        // If they stay in the same condition, use the cooldown so it doesn't spam.
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

    public void SetSourceEnabled(Entity<RadiationSourceComponent?> entity, bool val)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        entity.Comp.Enabled = val;
    }

    /// <summary>
    ///     Marks entity to receive/ignore radiation rays.
    /// </summary>
    public void SetCanReceive(EntityUid uid, bool canReceive)
    {
        if (canReceive)
        {
            EnsureComp<RadiationReceiverComponent>(uid);
        }
        else
        {
            RemComp<RadiationReceiverComponent>(uid);
        }
    }
}
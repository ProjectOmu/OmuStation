// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Server/_RMC14/Movement/RMCLagCompensationSystem.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Content.Omu.Common.CCVar;
using Content.Server.Movement.Systems;
using Content.Shared._RMC14.Movement;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Movement;

/// <summary>
///     Server half of lag compensation: forwards every coordinate lookup to
///     <see cref="LagCompensationSystem"/>, which owns the position history and does the rewind.
/// </summary>
public sealed class RMCLagCompensationSystem : SharedRMCLagCompensationSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly LagCompensationSystem _lagCompensation = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg,
            OmuCVars.LagCompensationMilliseconds,
            v => _lagCompensation.BufferTime = TimeSpan.FromMilliseconds(v),
            true);
    }

    public override (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid,
        ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        return _lagCompensation.GetCoordinatesAngle(uid, pSession, xform);
    }

    public override Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        return _lagCompensation.GetAngle(uid, session, xform);
    }

    public override EntityCoordinates GetCoordinates(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        return _lagCompensation.GetCoordinates(uid, session, xform);
    }
}

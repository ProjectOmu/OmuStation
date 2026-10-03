// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Shared/_RMC14/CCVar/RMCCVars.cs (RMCLagCompensationMilliseconds, RMCLagCompensationMarginTiles)
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Robust.Shared.Configuration;

namespace Content.Omu.Common.CCVar;

/// <summary>
///     CVars for ranged combat lag compensation.
///     These live in <c>Content.Omu.Common</c> because <c>Content.Shared</c>, <c>Content.Client</c>
///     and <c>Content.Server</c> all need to read them and <c>Content.Shared</c> cannot see
///     <c>Content.Omu.Shared</c>.
/// </summary>
public sealed partial class OmuCVars
{
    /// <summary>
    ///     How far back in time, in milliseconds, the server is willing to rewind a target's
    ///     position when adjudicating a shot for a player.
    ///     Doubles as the retention window of <c>LagCompensationComponent.Positions</c> and as the
    ///     upper bound on how stale a client-reported <c>LastRealTick</c> may be.
    /// </summary>
    public static readonly CVarDef<int> LagCompensationMilliseconds =
        CVarDef.Create("omu.lag_compensation_milliseconds", 750, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Rewind allowance, in milliseconds, that every session gets regardless of its measured ping.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     A client only gets to rewind as far back as its own connection can account for
    ///     (round-trip time times a factor); this is the floor under that, so a low-ping client is
    ///     not punished for the rate at which it is allowed to report its tick. The client's
    ///     heartbeat is throttled to one send per three ticks - about 100 ms at 30 tps - so a
    ///     perfectly honest claim is routinely that stale before jitter is counted.
    ///     </para>
    ///     <para>
    ///     Raising this towards <see cref="LagCompensationMilliseconds"/> weakens the plausibility
    ///     bound; at or above it, the bound is effectively off and any client may claim the deepest
    ///     rewind the server can serve. Integration tests pin it high deliberately, because a
    ///     loopback connection measures zero ping and would otherwise be held to this floor.
    ///     </para>
    /// </remarks>
    public static readonly CVarDef<int> LagCompensationMinRewindMilliseconds =
        CVarDef.Create("omu.lag_compensation_min_rewind_milliseconds", 150, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Extra slack, in tiles, allowed when testing a lag-compensated hit.
    ///     Keep this small: it is the only place where lag compensation is permitted to be
    ///     generous towards the shooter.
    /// </summary>
    public static readonly CVarDef<float> LagCompensationMarginTiles =
        CVarDef.Create("omu.lag_compensation_margin_tiles", 0.25f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Master switch for client-side gun prediction (the Phase 2 port).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <b>Defaults to false, unlike the source.</b> RMC-14 and ColonialMarinesUniverse ship this
    ///     on because prediction is load-bearing for their whole combat design. On Omu it is a new,
    ///     high-blast-radius feature layered onto a gun path that three forks have already modified,
    ///     and the only way to actually evaluate it is a live multiplayer session with real latency.
    ///     Shipping it off means the port can land, be reviewed and be reverted by a CVar rather than
    ///     a revert commit, and means the server-side lag compensation from Phase 1 is what is being
    ///     measured until someone deliberately turns this on.
    ///     </para>
    ///     <para>
    ///     When false the client sends no predicted projectile ids, the server spawns and adjudicates
    ///     every projectile exactly as it did before this port, and the prediction systems idle.
    ///     </para>
    /// </remarks>
    public static readonly CVarDef<bool> GunPrediction =
        CVarDef.Create("omu.gun_prediction", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Whether a server-spawned predicted projectile should refuse to collide with a
    ///     lag-compensated entity that the rewind says it did not actually reach.
    /// </summary>
    /// <remarks>
    ///     Off by default, as in the source. This suppresses the engine's own collision in favour of
    ///     the rewound adjudication, which is the more invasive of the two ways to resolve a
    ///     disagreement between the two adjudicators; leave it off unless double-damage is observed.
    /// </remarks>
    public static readonly CVarDef<bool> GunPredictionPreventCollision =
        CVarDef.Create("omu.gun_prediction_prevent_collision", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Log every accepted and rejected predicted hit. Debugging aid; spams at fire rate.
    /// </summary>
    public static readonly CVarDef<bool> GunPredictionLogHits =
        CVarDef.Create("omu.gun_prediction_log_hits", false, CVar.SERVER | CVar.REPLICATED);

    // Omu - gun prediction port (Phase 2): three CVars lived here and have been removed.
    //   omu.gun_prediction_coordinate_deviation
    //   omu.gun_prediction_lowest_coordinate_deviation
    //   omu.gun_prediction_aabb_enlargement
    // They tuned Path A, the ping-based adjudicator this port originally copied from RMC-14. Guns
    // now use Path B instead - SharedRMCLagCompensationSystem.Collides, which Phase 1 shipped for
    // exactly this and which the port plan asked for. Path B derives the geometry server-side from
    // the clamped reported tick and needs no client-position tolerance and no AABB inflation, so
    // there is nothing left for these three to tune. Its one knob is omu.lag_compensation_margin_tiles.
}

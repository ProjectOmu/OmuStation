// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing; // Omu - gun prediction port: GameTick, for LastRealTick

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on the client to indicate it'd like to shoot.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestShootEvent : EntityEventArgs
{
    public NetEntity Gun;
    public NetCoordinates Coordinates;
    public NetEntity? Target;

    // Omu start - gun prediction port (Phase 2).

    /// <summary>
    /// Ids of the client-side projectile entities this client already spawned for this shot, in the
    /// order they were spawned, or null when this shot was not predicted - either because
    /// <c>omu.gun_prediction</c> is off or because <c>PredictShot</c> declined this particular shot
    /// (multishot gun, carried shooter, nothing drawable). Unlike <see cref="LastRealTick"/>, this
    /// field really is only populated when the client is predicting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are raw <see cref="EntityUid.Id"/> values from the <b>client's</b> entity space, not
    /// <see cref="NetEntity"/>s - a client-side entity has no network id by definition. The server
    /// never resolves them to entities; it only stores one on each projectile it spawns so that the
    /// originating client can later be told "the server projectile matching your id N is gone, delete
    /// your copy". Treat them as opaque correlation tokens scoped to one session.
    /// </para>
    /// <para>
    /// This is untrusted client input like every other field here. A client can send any ids it
    /// likes; the worst it achieves is making its own copies disappear.
    /// </para>
    /// </remarks>
    public List<int>? Shot;

    /// <summary>
    /// The last tick this client had authoritative server state for, used to rewind targets to
    /// where the shooter was actually seeing them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always populated, whether or not prediction is on.</b> The client fills this
    /// unconditionally from <c>IGameTiming.LastRealTick</c> in
    /// <c>Content.Client.Weapons.Ranged.Systems.GunSystem</c>, outside the <c>omu.gun_prediction</c>
    /// guard that decides whether <see cref="Shot"/> is filled - so a populated value here says
    /// nothing about whether the shooter is predicting, and an unset value must not be read as "off".
    /// The gate is on the receiving side instead: the server's <c>GunSystem.OnShootRequestReceived</c>
    /// applies this only while <c>omu.gun_prediction</c> is on, so that with the feature off the
    /// per-session store is fed by the lag-compensation heartbeat alone, exactly as before the port.
    /// See the remarks on that override for why that matters.
    /// </para>
    /// <para>
    /// Untrusted. It is clamped into the lag-compensation buffer at the single point it is written
    /// (<c>SharedRMCLagCompensationSystem.SetLastRealTick</c>: never ahead of the server, never older
    /// than the position history), so nothing downstream can be handed a rewind depth the history
    /// could not satisfy.
    /// </para>
    /// <para>
    /// What it does <i>not</i> reach: the predicted-projectile adjudicator in
    /// <c>Content.Server._RMC14.Weapons.Ranged.Prediction.GunPredictionSystem.Collides</c> never
    /// consults it - that path rewinds by the server's own measured channel ping instead. This value
    /// is consumed by <c>Content.Server.Movement.Systems.LagCompensationSystem.GetCoordinatesAngle</c>,
    /// i.e. the melee and hitscan rewind paths.
    /// </para>
    /// </remarks>
    public GameTick LastRealTick;

    /// <summary>
    /// Which physics substep within <see cref="LastRealTick"/> the client was on, for rewinds finer
    /// than a whole tick.
    /// </summary>
    /// <remarks>
    /// Sent alongside the tick because the two are only meaningful together. The lag-compensation
    /// heartbeat has always carried both; the shoot request originally carried only the tick, which
    /// meant applying it silently reset the substep to zero and discarded the heartbeat's precision
    /// for that session. Clamped to one tick's worth in either direction at the same single write
    /// point as the tick itself.
    /// </remarks>
    public int LastRealSubstep;

    // Omu end
}

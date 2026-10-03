// SPDX-FileCopyrightText: 2024 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors
// SPDX-FileCopyrightText: 2026 Omu Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Ported from RMC-14 via ColonialMarinesUniverse.
// Source: Content.Client/_RMC14/Weapons/Ranged/Prediction/GunPredictionSystem.cs
// Lineage: Space Station 14 -> RMC-14 -> ColonialMarinesUniverse -> Omu Station.

using Content.Client.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Projectiles;
using Robust.Client.GameObjects;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Weapons.Ranged.Prediction;

/// <summary>
///     Client half of gun prediction: keeps the shooter's own speculative projectile copies stable
///     across reprediction, reports the hits they make so the server can adjudicate them, and hides
///     the authoritative projectile that comes back for a shot the shooter already drew.
/// </summary>
/// <remarks>
///     <para>
///     <b>Deviations from the source</b> (RMC-14 / ColonialMarinesUniverse):
///     </para>
///     <list type="bullet">
///         <item>
///         <b>No <c>RequestShootEvent</c> subscription.</b> The source subscribed to it here (and in
///         the server system) and called <c>SharedGunPredictionSystem.ShootRequested</c>. Omu's
///         <c>SharedGunSystem</c> already owns that event and applies four fork-specific rules to it,
///         so Omu's <c>SharedGunPredictionSystem</c> deliberately has no <c>ShootRequested</c> at all
///         - a second subscriber would fire the gun twice, because the bus keeps dispatching after
///         the first handler. Nothing replaces it on the client: the client already knows which
///         copies it spawned, and the ids it sends travel with the shoot request itself.
///         </item>
///         <item>
///         <b>No warlock-shield coupling.</b> The source kept an
///         <c>EntityQuery&lt;CMUXenoFrozenProjectileComponent&gt;</c> and fed it to a static
///         <c>ShouldRestorePredictedProjectileCoordinates(bool frozenByWarlockShield)</c> so that a
///         projectile frozen in mid-air by a warlock shield would not be yanked back to its
///         pre-solve position each tick. Omu has no warlock shield and no such component. With the
///         only argument gone the helper degenerated to <c>return true</c>, so it has been
///         <b>deleted</b> and its (now unconditional) behaviour inlined into
///         <see cref="OnAfterSolve"/>. Re-introduce the helper, not just the query, if a
///         freeze-a-projectile-in-place effect is ever ported.
///         </item>
///         <item>
///         <b>Every observable action is gated on
///         <see cref="SharedGunPredictionSystem.GunPrediction"/></b>, because <c>omu.gun_prediction</c>
///         defaults to <c>false</c> here where the source ships it on. Each gate is documented where
///         it appears.
///         </item>
///     </list>
/// </remarks>
public sealed partial class GunPredictionSystem : SharedGunPredictionSystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    // Deviation: Omu declares ProjectileCollide on the concrete client ProjectileSystem, matching the
    // server side, rather than on SharedProjectileSystem as RMC-14 does.
    [Dependency] private readonly ProjectileSystem _projectile = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<IgnorePredictionHideComponent> _ignorePredictionHideQuery;
    private EntityQuery<IgnorePredictionHitComponent> _ignorePredictionHitQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _ignorePredictionHideQuery = GetEntityQuery<IgnorePredictionHideComponent>();
        _ignorePredictionHitQuery = GetEntityQuery<IgnorePredictionHitComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);

        // Deviation: no SubscribeLocalEvent<RequestShootEvent>. See the class remarks.

        SubscribeLocalEvent<PredictedProjectileClientComponent, UpdateIsPredictedEvent>(OnClientProjectileUpdateIsPredicted);
        SubscribeLocalEvent<PredictedProjectileClientComponent, StartCollideEvent>(OnClientProjectileStartCollide);

        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentStartup>(OnServerProjectileStartup);
        // CMU14
        SubscribeLocalEvent<PredictedProjectileServerComponent, ComponentRemove>(OnServerProjectileRemove);
        // CMU14

        UpdatesBefore.Add(typeof(TransformSystem));
    }

    /// <remarks>
    ///     Snapshots where each predicted copy sat before the physics solve, so
    ///     <see cref="OnAfterSolve"/> can put it back during reprediction.
    ///     <para>
    ///     Gate 1 (client): skipped entirely when prediction is off. The snapshot and the restore
    ///     must be gated identically - if one ran without the other, a copy could be restored to a
    ///     position recorded arbitrarily far in the past.
    ///     </para>
    /// </remarks>
    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        if (!GunPrediction)
            return;

        var query = EntityQueryEnumerator<PredictedProjectileClientComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            predicted.Coordinates = Transform(uid).Coordinates;
        }
    }

    /// <remarks>
    ///     Undoes the solve for predicted copies on every pass except the first-time-predicted one,
    ///     so repeated reprediction of the same tick does not advance a copy several tick's worth of
    ///     travel.
    ///     <para>
    ///     Gate 2 (client): paired with <see cref="OnBeforeSolve"/>; see the note there.
    ///     </para>
    ///     <para>
    ///     Deviation: RMC-14 consulted
    ///     <c>ShouldRestorePredictedProjectileCoordinates(_warlockFrozenProjectileQuery.HasComp(uid))</c>
    ///     here and, when it said no, cleared the snapshot without restoring so the frozen projectile
    ///     stayed where the shield had pinned it. Omu has no such effect, that helper degenerated to
    ///     a constant <c>true</c>, and it has been deleted - the restore below is what remains of it,
    ///     now unconditional.
    ///     </para>
    /// </remarks>
    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent ev)
    {
        if (!GunPrediction)
            return;

        if (_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<PredictedProjectileClientComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            if (predicted.Coordinates is { } coordinates)
                _transform.SetCoordinates(uid, coordinates);

            predicted.Coordinates = null;
        }
    }

    /// <summary>
    ///     Whether a server projectile arriving for the local player should retire the client-side
    ///     copy that was standing in for it.
    /// </summary>
    /// <remarks>
    ///     Kept from the CMU14 additions and left as a pure static so it stays directly testable: all
    ///     four conditions must hold, so the client never deletes an entity that is not its own
    ///     short-lived predicted copy.
    /// </remarks>
    public static bool ShouldRetirePredictedProjectileCopy(
        bool serverProjectileBelongsToLocalPlayer,
        bool clientCopyExists,
        bool clientCopyIsClientSide,
        bool clientCopyIsPredicted)
    {
        return serverProjectileBelongsToLocalPlayer &&
               clientCopyExists &&
               clientCopyIsClientSide &&
               clientCopyIsPredicted;
    }

    private void OnClientProjectileUpdateIsPredicted(Entity<PredictedProjectileClientComponent> ent, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }

    private void OnClientProjectileStartCollide(Entity<PredictedProjectileClientComponent> ent, ref StartCollideEvent args)
    {
        if (_timing.ApplyingState || ent.Comp.Hit)
            return;

        if (!TryComp(ent, out ProjectileComponent? projectile) ||
            !TryComp(ent, out PhysicsComponent? physics) ||
            _ignorePredictionHitQuery.HasComp(args.OtherEntity) ||
            !IsSameMap(ent.Owner, args.OtherEntity))
        {
            return;
        }

        var netEnt = GetNetEntity(args.OtherEntity);
        var pos = _transform.GetMapCoordinates(args.OtherEntity);
        var hit = new HashSet<(NetEntity, MapCoordinates)> { (netEnt, pos) };
        PredictHit(ent, projectile, physics, args.OtherEntity, hit);
    }

    private void OnServerProjectileStartup(Entity<PredictedProjectileServerComponent> ent, ref ComponentStartup args)
    {
        // Gate 3 (client): with prediction off no sprite is ever hidden. Belt and braces - with the
        // CVar off the server never attaches this component in the first place, so this handler
        // should not fire at all.
        if (!GunPrediction)
            return;

        if (ent.Comp.ClientEnt != _player.LocalEntity)
            return;

        if (_ignorePredictionHideQuery.HasComp(ent))
            return;

        if (_spriteQuery.TryComp(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), false);
    }

    // CMU14
    private void OnServerProjectileRemove(Entity<PredictedProjectileServerComponent> ent, ref ComponentRemove args)
    {
        var localProjectile = ent.Comp.ClientEnt == _player.LocalEntity;
        if (!localProjectile)
            return;

        // Gate 4 (client): retiring the stand-in copy deletes an entity, so it is gated. With
        // prediction off there is no copy to retire.
        if (GunPrediction)
            RetirePredictedProjectileCopy(ent.Comp.ClientId, localProjectile);

        if (_ignorePredictionHideQuery.HasComp(ent))
            return;

        // Deviation: the restore is deliberately NOT behind the GunPrediction gate, where the source
        // put the whole method behind it. Hiding happens in OnServerProjectileStartup; if the CVar is
        // turned off while a hidden projectile is still in flight, a gated restore would leave that
        // projectile permanently invisible. Making something visible is never the unsafe direction,
        // so this side of the pair runs unconditionally.
        if (_spriteQuery.TryComp(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), true);
    }

    private void RetirePredictedProjectileCopy(int clientId, bool localProjectile)
    {
        var predicted = new EntityUid(clientId);
        var exists = Exists(predicted);
        var isClientSide = exists && IsClientSide(predicted);
        var isPredicted = exists && HasComp<PredictedProjectileClientComponent>(predicted);
        if (!ShouldRetirePredictedProjectileCopy(localProjectile, exists, isClientSide, isPredicted))
            return;

        QueueDel(predicted);
    }
    // CMU14

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Gate 5 (client): both loops below are prediction-only behaviour - one claims hits, the
        // other hides sprites - so neither runs when the feature is off.
        if (!GunPrediction)
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        // TODO gun prediction remove this once the client reliably detects collisions
        var projectiles = EntityQueryEnumerator<PredictedProjectileClientComponent, ProjectileComponent, PhysicsComponent>();
        while (projectiles.MoveNext(out var uid, out var predicted, out var projectile, out var physics))
        {
            if (predicted.Hit)
                continue;

            var contacts = _physics.GetContactingEntities(uid, physics, true);
            if (contacts.Count == 0)
                continue;

            var hit = new HashSet<(NetEntity, MapCoordinates)>();
            EntityUid? firstHit = null;
            foreach (var contact in contacts)
            {
                if (_ignorePredictionHitQuery.HasComp(contact) ||
                    !IsSameMap(uid, contact))
                {
                    continue;
                }

                var netEnt = GetNetEntity(contact);
                var pos = _transform.GetMapCoordinates(contact);
                hit.Add((netEnt, pos));
                firstHit ??= contact;
            }

            if (firstHit is not { } firstHitEntity)
                continue;

            PredictHit((uid, predicted), projectile, physics, firstHitEntity, hit);
        }

        var predictedQuery = EntityQueryEnumerator<PredictedProjectileHitComponent, SpriteComponent, TransformComponent>();
        while (predictedQuery.MoveNext(out var uid, out var hit, out var sprite, out var xform))
        {
            var origin = hit.Origin;
            var coordinates = xform.Coordinates;
            if (!origin.TryDistance(EntityManager, _transform, coordinates, out var distance) ||
                distance >= hit.Distance)
            {
                _sprite.SetVisible((uid, sprite), false);
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // No CVar gate needed: this only touches entities carrying PredictedProjectileClientComponent,
        // which only exist while prediction is on, and turning lerping off for a leftover copy has no
        // observable effect beyond the copy itself.
        // TODO bullet prediction remove this when lerping doesnt make the client's entity slightly slower
        var projectiles = EntityQueryEnumerator<PredictedProjectileClientComponent, TransformComponent>();
        while (projectiles.MoveNext(out _, out var xform))
        {
            xform.ActivelyLerping = false;
        }
    }

    /// <summary>
    ///     Latches a predicted copy as spent, tells the server what it thinks it hit, and runs the
    ///     collision locally so the shooter gets immediate feedback.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Gate 6 (client): the single choke point for both call sites. Nothing is reported and no
    ///     local collision is run when prediction is off, so the server sees no hit events from this
    ///     client at all.
    ///     </para>
    ///     <para>
    ///     The <see cref="PredictedProjectileHitEvent"/> raised here is the untrusted input the server
    ///     re-derives in <c>Content.Server._RMC14.Weapons.Ranged.Prediction.GunPredictionSystem</c>.
    ///     Nothing sent from here is authoritative: the server scopes the projectile id to this
    ///     session, latches it single-use, and re-tests every claimed target against its own rewound
    ///     positions.
    ///     </para>
    /// </remarks>
    private void PredictHit(
        Entity<PredictedProjectileClientComponent> ent,
        ProjectileComponent projectile,
        PhysicsComponent physics,
        EntityUid firstHit,
        HashSet<(NetEntity Id, MapCoordinates Coordinates)> hit)
    {
        if (!GunPrediction)
            return;

        if (ent.Comp.Hit)
            return;

        ent.Comp.Hit = true;

        var ev = new PredictedProjectileHitEvent(ent.Owner.Id, hit);
        RaiseNetworkEvent(ev);

        // Keep predicted hits on the normal collision path. A previous manual effect path
        // skipped local damage flashes and made shooter feedback wait for server state.
        _projectile.ProjectileCollide((ent, projectile, physics), firstHit);
    }
}

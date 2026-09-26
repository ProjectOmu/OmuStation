// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Components; // Omu - PhysicsComponent for ProjectileCollide
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Client.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly AnimationPlayerSystem _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImpactEffectEvent>(OnProjectileImpact);
    }

    // Omu start - client-side counterpart of Content.Server/Projectiles/ProjectileSystem.ProjectileCollide,
    // for gun prediction. This is NOT a port of the server method: it only does what the client may do
    // locally without fighting the server's authoritative state.
    /// <summary>
    ///     Marks a locally predicted projectile as having hit <paramref name="target"/>.
    /// </summary>
    /// <remarks>
    ///     Deliberately does far less than the server:
    ///     <list type="bullet">
    ///         <item>No damage, no destructible/penetration maths, no colour flash and no admin log - the
    ///         client cannot know the real outcome (resistances, armour rolls, destruction), and the server
    ///         already replicates the damage state and sends the flash to everyone in PVS.</item>
    ///         <item>No <see cref="ProjectileReflectAttemptEvent"/> - reflection is a random roll that also
    ///         mutates the projectile's velocity and rotation, so predicting it would desync the local copy
    ///         and can fire off reflect sounds/popups for a reflect the server never rolled.</item>
    ///         <item>No <c>ProjectileHitEvent</c> - its handlers (e.g. embedding) reparent and re-body the
    ///         projectile, which is server state.</item>
    ///         <item>No impact sound (the client override of PlayImpactSound is a no-op anyway), no camera
    ///         kick (the server kicks the <em>target's</em> camera, not the shooter's) and no impact effect
    ///         (the server raises ImpactEffectEvent to everyone in PVS, including us, so predicting it here
    ///         would spawn the effect twice).</item>
    ///         <item>No deletion - the projectile is a networked entity owned by the server; deleting it
    ///         client-side would just have it come back with the next game state.</item>
    ///     </list>
    ///     What is left is marking the projectile spent so the same local copy cannot "hit" over and over
    ///     while the claimed hit is in flight to the server.
    /// </remarks>
    /// <param name="predicted">
    ///     True when the call came from the prediction layer rather than from physics. Unused here: this
    ///     method is only ever reached on a predicted hit in the first place, so there is nothing that has
    ///     to behave differently. Kept so both sides share one signature and callers can pass it.
    /// </param>
    public void ProjectileCollide(Entity<ProjectileComponent, PhysicsComponent> projectile, EntityUid target, bool predicted = false)
    {
        var component = projectile.Comp1;

        if (component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        component.ProjectileSpent = true;
    }
    // Omu end

    private void OnProjectileImpact(ImpactEffectEvent ev)
    {
        var coords = GetCoordinates(ev.Coordinates);

        if (Deleted(coords.EntityId))
            return;

        var ent = Spawn(ev.Prototype, coords);

        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            sprite[EffectLayers.Unshaded].AutoAnimated = false;
            _sprite.LayerMapTryGet((ent, sprite), EffectLayers.Unshaded, out var layer, false);
            var state = _sprite.LayerGetRsiState((ent, sprite), layer);
            var lifetime = 0.5f;

            if (TryComp<TimedDespawnComponent>(ent, out var despawn))
                lifetime = despawn.Lifetime;

            var anim = new Animation()
            {
                Length = TimeSpan.FromSeconds(lifetime),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick()
                    {
                        LayerKey = EffectLayers.Unshaded,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state.Name, 0f),
                        }
                    }
                }
            };

            _player.Play(ent, anim, "impact-effect");
        }
    }
}
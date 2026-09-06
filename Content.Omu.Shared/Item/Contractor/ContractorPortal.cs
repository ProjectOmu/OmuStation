using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using System.Linq;
using Content.Goobstation.Common.BlockTeleport;
using Content.Shared.Ghost;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Teleportation.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Omu.Shared.Item.Contractor
{

    [RegisterComponent, NetworkedComponent]
    public sealed partial class ContractorPortalComponent : Component
    {
        /// <summary>
        ///     Sound played on departing from this portal, centered on the original portal.
        /// </summary>
        [DataField("departureSound")]
        public SoundSpecifier DepartureSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");

        /// <summary>
        ///     If no portals are linked, the subject will be teleported a random distance at maximum this far away.
        /// </summary>
        [DataField("maxRandomRadius"), ViewVariables(VVAccess.ReadWrite)]
        public float MaxRandomRadius = 14.0f;

        /// <summary>
        ///     Maximum distance that portals can teleport to, in all cases. Mostly this matters for linked portals.
        ///     Null means no restriction on distance.
        /// </summary>
        /// <remarks>
        ///     Obviously this should strictly be larger than <see cref="MaxRandomRadius"/> (or null)
        /// </remarks>
        [DataField("maxTeleportRadius"), ViewVariables(VVAccess.ReadWrite)]
        public float? MaxTeleportRadius = 20f;

        [DataField("TargetEntity"), ViewVariables(VVAccess.ReadWrite)]
        public EntityUid? TargetEntity;

        [DataField("Reward"), ViewVariables(VVAccess.ReadWrite)]
        public string? Reward;
    }

    public sealed class ContractorPortalSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly PullingSystem _pulling = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;

        private const string PortalFixture = "portalFixture";
        private const string ProjectileFixture = "projectile";

        private const int MaxRandomTeleportAttempts = 20;

        private const string CommandTarget = "Telecrystal50";

        /// <inheritdoc/>
        public override void Initialize()
        {
            SubscribeLocalEvent<ContractorPortalComponent, StartCollideEvent>(OnCollide);
        }

        private void OnCollide(Entity<ContractorPortalComponent> ent, ref StartCollideEvent args)
        {
            if (!ShouldCollide(args.OurFixtureId, args.OtherFixtureId, args.OurFixture, args.OtherFixture))
                return;

            var subject = args.OtherEntity;

            if (ent.Comp.TargetEntity != subject)
            {
                return;
            }

            // best not.
            if (Transform(subject).Anchored)
                return;

            // break pulls before portal enter so we don't break shit
            if (TryComp<PullableComponent>(subject, out var pullable) && pullable.BeingPulled)
            {
                _pulling.TryStopPull(subject, pullable, ignoreGrab: true); // Goobstation edit
            }

            if (TryComp<PullerComponent>(subject, out var pullerComp)
                && TryComp<PullableComponent>(pullerComp.Pulling, out var subjectPulling))
            {
                _pulling.TryStopPull(pullerComp.Pulling.Value, subjectPulling, ignoreGrab: true); // Goobstation edit
            }

            // if they came from another portal, just return and wait for them to exit the portal
            if (HasComp<PortalTimeoutComponent>(subject))
            {
                return;
            }

            //if (TryComp<LinkedEntityComponent>(ent, out var link))
            //{
            //    if (link.LinkedEntities.Count == 0)
            //        return;

            //    // check prediction
            //    if (_netMan.IsClient && !CanPredictTeleport((ent, link)))
            //        return;

            //    // pick a target and teleport there
            //    var target = _random.Pick(link.LinkedEntities);

            //    if (HasComp<PortalComponent>(target))
            //    {
            //        // if target is a portal, signal that they shouldn't be immediately teleported back
            //        var timeout = EnsureComp<PortalTimeoutComponent>(subject);
            //        timeout.EnteredPortal = ent;
            //        Dirty(subject, timeout);
            //    }

            //    TeleportEntity(ent, subject, Transform(target).Coordinates, target);
            //    return;
            //}

            if (_netMan.IsClient)
                return;

            TeleportRandomly(ent, subject);
        }
        private bool ShouldCollide(string ourId, string otherId, Fixture our, Fixture other)
        {
            return ourId == PortalFixture && (other.Hard || otherId == ProjectileFixture);
        }
        private void TeleportRandomly(Entity<ContractorPortalComponent> ent, EntityUid subject)
        {
            var xform = Transform(ent);
            var coords = xform.Coordinates;
            var newCoords = coords.Offset(_random.NextVector2(ent.Comp.MaxRandomRadius));
            for (var i = 0; i < MaxRandomTeleportAttempts; i++)
            {
                var randVector = _random.NextVector2(ent.Comp.MaxRandomRadius);
                newCoords = coords.Offset(randVector);
                if (!_lookup.AnyEntitiesIntersecting(_transform.ToMapCoordinates(newCoords), LookupFlags.Static))
                {
                    // newCoords is not a wall
                    break;
                }
                // after "MaxRandomTeleportAttempts" attempts, end up in the walls
            }

            TeleportEntity(ent, subject, newCoords);
        }
        private void TeleportEntity(Entity<ContractorPortalComponent> ent, EntityUid subject, EntityCoordinates target, EntityUid? targetEntity = null, bool playSound = true)
        {
            var departureSound = ent.Comp.DepartureSound;

            var ev = new TeleportAttemptEvent(false);
            RaiseLocalEvent(subject, ref ev);
            if (ev.Cancelled)
                return;

            var tc = ent.Comp.Reward;

            if (tc is null)
            {
                tc = "Telecrystal25";
            }
            var coords = Transform(subject);

            PredictedSpawnAtPosition(tc, coords.Coordinates);
            _transform.SetCoordinates(subject, target);

            _popup.PopupEntity("Extraction successful!", ent);

            if (!playSound)
                return;

            _audio.PlayPredicted(departureSound, ent, subject);
            PredictedQueueDel(ent);

        }
    }
}

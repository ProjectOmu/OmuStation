using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using System.Linq;
using Content.Goobstation.Common.BlockTeleport;
using Content.Shared.Ghost;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.DoAfter;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Content.Goobstation.Common.Interactions;
using Content.Shared.Interaction.Events;
using Content.Shared.Humanoid;

namespace Content.Omu.Shared.Item.Contractor
{

    [RegisterComponent, NetworkedComponent]
    public sealed partial class ContractorPDAComponent : Component
    {

        [DataField("TargetEntity"), ViewVariables(VVAccess.ReadWrite)]
        public EntityUid? TargetEntity;

        [DataField("Reward"), ViewVariables(VVAccess.ReadWrite)]
        public string? Reward;

        [DataField("DoAfterDuration"), ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(10);

    }

    public sealed class ContractorPDASystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private IRobustRandom _random = default!;
        public override void Initialize()
        {
            SubscribeLocalEvent<ContractorPDAComponent, UseInHandEvent>(OnUse);
            SubscribeLocalEvent<ContractorPDAComponent, ContractorExtractDoAfterEvent>(OnDoAfter);
            base.Initialize();
        }

        private void OnUse(Entity<ContractorPDAComponent> ent, ref UseInHandEvent args)
        {
            var doAfterArgs = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.DoAfterDuration,
            new ContractorExtractDoAfterEvent(),
            ent)
            {
                NeedHand = true,
                BreakOnDamage = false,
                DistanceThreshold = 1,
                MovementThreshold = 1f,
                BreakOnHandChange = false,
            };
            _doAfter.TryStartDoAfter(doAfterArgs);
        }

        private void OnDoAfter(Entity<ContractorPDAComponent> ent, ref ContractorExtractDoAfterEvent args)
        {
            if (ent.Comp.TargetEntity is not null)
            {
                var xform = Transform(args.User);
                var portal = PredictedSpawnAtPosition("PortalContractor", xform.Coordinates);

                if (TryComp<ContractorPortalComponent>(portal, out var portComp) && ent.Comp.TargetEntity is not null)
                {
                    portComp.TargetEntity = ent.Comp.TargetEntity;
                    portComp.Reward = ent.Comp.Reward;
                }
            }
            var newTarget = SelectTarget(ent);
            if (newTarget == EntityUid.Invalid)
                return;

            ent.Comp.TargetEntity = newTarget;
        }

        private EntityUid SelectTarget(Entity<ContractorPDAComponent> ent)
        {
            var targets = new List<EntityUid>();
            var query = EntityQueryEnumerator<HumanoidAppearanceComponent>();

            while (query.MoveNext(out var uid, out _))
            {
                targets.Add(uid);
            }

            if (targets.Count == 0)
                return EntityUid.Invalid;

            return _random.Pick(targets);
        }
    }

    [Serializable, NetSerializable]
    public sealed partial class ContractorExtractDoAfterEvent : SimpleDoAfterEvent;
}

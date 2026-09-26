using System.Reflection.Metadata;
using Content.Shared._Mono.Claws.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Claws;
    public sealed partial class ClawsGrowthEntityEffectSystem : EntityEffectSystem<ClawsComponent, ClawsGrowth>
    {
        protected override void Effect(Entity<ClawsComponent> entity, ref EntityEffectEvent<ClawsGrowth> args)
        {
                var sys = EntityManager.EntitySysManager.GetEntitySystem<SharedClawsSystem>();
                var growth = args.Effect.Growth;

                sys.GrowClaws(TimeSpan.FromSeconds(growth), entity);
        }
    }
    public sealed partial class ClawsGrowth : EntityEffectBase<ClawsGrowth>
    {

        /// <summary>
        /// Bonus Claws growth in seconds. X seconds of additional growth per second.
        /// </summary>
        [DataField]
        public double Growth;

        public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
            => Loc.GetString("reagent-effect-guidebook-claws-growth",
                ("chance", Probability),
                ("amount", Growth));

    }


// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Behaviors;
using JetBrains.Annotations;

namespace Content.Server.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class SpillBehavior : IThresholdBehavior
{
    /// <summary>
    /// Optional fallback solution name if SpillableComponent is not present.
    /// </summary>
    [DataField]
    public string? Solution;

        /// <summary>
        /// If there is a SpillableComponent on EntityUidowner use it to create a puddle/smear.
        /// Or whatever solution is specified in the behavior itself.
        /// If none are available do nothing.
        /// </summary>
        /// <param name="owner">Entity on which behavior is executed</param>
        /// <param name="system">system calling the behavior</param>
        /// <param name="cause"></param>
        public void Execute(EntityUid owner, SharedDestructibleSystem system, EntityUid? cause = null)
        {
            var puddleSystem = system.EntityManager.System<PuddleSystem>();
            var solutionContainer = system.EntityManager.System<SharedSolutionContainerSystem>();
            var coordinates = system.EntityManager.GetComponent<TransformComponent>(owner).Coordinates;

        // Spill the solution that was drained/split
        if (solutionContainer.TryGetSolution(owner, Solution, out _, out var solution))
            puddleSystem.TrySplashSpillAt(owner, coordinates, solution, out _, false, cause);
        else
            puddleSystem.TrySplashSpillAt(owner, coordinates, out _, out _, false, cause);
    }
}

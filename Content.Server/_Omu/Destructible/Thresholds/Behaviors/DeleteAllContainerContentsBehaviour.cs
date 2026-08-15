using Robust.Shared.Containers;

namespace Content.Server.Destructible.Thresholds.Behaviors
{
    /// <summary>
    ///     Delete all items from all containers
    /// </summary>
    [DataDefinition]
    public sealed partial class DeleteAllContainerContentsBehaviour : IThresholdBehavior
    {
        public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
        {
            if (!system.EntityManager.TryGetComponent<ContainerManagerComponent>(owner, out var containerManager))
                return;

            foreach (var container in system.EntityManager.System<SharedContainerSystem>().GetAllContainers(owner, containerManager))
            {
                system.ContainerSystem.CleanContainer(container);
            }
        }
    }
}

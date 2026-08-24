using Robust.Shared.Containers;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Destructible.Thresholds.Behaviors
{
    /// <summary>
    ///     Delete all items from all containers if the entity has the "NotOpenedSecureCrate" tag.
    /// </summary>
    [DataDefinition]
    public sealed partial class DeleteAllLockedContainerContentsBehaviour : IThresholdBehavior
    {
        private static readonly ProtoId<TagPrototype> NotOpenedSecureCrateTag = "NotOpenedSecureCrate";
        public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
        {
            if (!system.TagSystem.HasTag(owner, NotOpenedSecureCrateTag))
                return;

            if (!system.EntityManager.TryGetComponent<ContainerManagerComponent>(owner, out var containerManager))
                return;

            foreach (var container in system.EntityManager.System<SharedContainerSystem>().GetAllContainers(owner, containerManager))
            {
                system.ContainerSystem.CleanContainer(container);
            }
        }
    }
}

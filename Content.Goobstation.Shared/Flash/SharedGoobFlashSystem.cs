// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Flash;
using Content.Goobstation.Shared.Overlays;
using Content.Shared.Inventory;

namespace Content.Goobstation.Shared.Flash;

public sealed class SharedGoobFlashSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlashVulnerableComponent, CheckFlashVulnerable>(OnFlashVulnerableCheck);
    }

    public void OnFlashVulnerableCheck(Entity<FlashVulnerableComponent> ent, ref CheckFlashVulnerable args)
    {
        // Omu begin
        if (ent.Comp.OnlyTriggerWhenNvIsOnWithoutNvGoggles)
        {
            args.Vulnerable = CheckFlashVulnerabilityWithNvGoggles(ent, args);
            return;
        }
        // Omu end
        args.Vulnerable = true;
    }

    // Omu begin
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private bool CheckFlashVulnerabilityWithNvGoggles(Entity<FlashVulnerableComponent> ent, CheckFlashVulnerable args)
    {
        if (!_entityManager.TryGetComponent(ent, out NightVisionComponent? nightVisionNullable))
            return true;

        if (!nightVisionNullable.IsActive)
            return false;

        return !CheckForNightVisionItemsInInventory(ent);
    }

    private bool CheckForNightVisionItemsInInventory(Entity<FlashVulnerableComponent> ent)
    {
        return _inventory.TryGetSlotContainer(ent.Owner, "eyes", out var eyesContainer, out _) && _entityManager.HasComponent<NightVisionComponent>(eyesContainer.ContainedEntity);
    }
    // Omu end
}

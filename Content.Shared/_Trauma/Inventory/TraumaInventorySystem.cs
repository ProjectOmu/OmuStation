// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Shared._Trauma.Tackle;

namespace Content.Shared._Trauma.Inventory;

public sealed class TraumaInventorySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, TackleEvent>(_inventory.RelayEvent);
    }
}

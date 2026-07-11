using Content.Goobstation.Common.Speech;
using Content.Omu.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Traits;

/// <summary>
///     info.
/// </summary>
public sealed class CyberneticBeastSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberneticBeastComponent, GetSpeechSoundEvent>(OnSpeechSoundNeeded);
    }

    /// <summary>
    ///     Changes this cybernetic beast's speech sound while wearing a mantle. The specific sound that gets played can be specified in the CyberneticBeastComponent.
    /// </summary>
    private void OnSpeechSoundNeeded(Entity<CyberneticBeastComponent> ent, ref GetSpeechSoundEvent args)
    {
        if (IsWearingMantle(ent))
            args.SpeechSoundProtoId = ent.Comp.SpeechSoundWhileWearingMantle;
    }

    /// <summary>
    ///     Whether the specified cybernetic beast is wearing a mantle.
    /// </summary>
    private bool IsWearingMantle(Entity<CyberneticBeastComponent> ent)
    {
        // if the passed entity has an inventory component, and in that inventory one item has a cybernetic mantle component, the player must be wearing a cybernetic mantle.
        if (EntityManager.TryGetComponent<InventoryComponent>(ent, out var inventoryComponent))
            if (_inventorySystem.TryGetInventoryEntity<CyberneticMantleComponent>(new Entity<InventoryComponent?>(ent, inventoryComponent), out var cyberneticMantle))
                return true;

        return false;
    }

}
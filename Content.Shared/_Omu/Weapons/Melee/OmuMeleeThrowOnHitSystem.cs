using Content.Shared._Omu.Weapons.Melee.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Weapons.Melee.Components;

namespace Content.Shared._Omu.Weapons.Melee;

/// <summary>
/// This handles <see cref="OmuMeleeThrowOnHitComponent"/>
/// </summary>
public sealed class OmuMeleeThrowOnHitSystem : EntitySystem
{

    [Dependency] private readonly EmagSystem _emag = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<OmuMeleeThrowOnHitComponent, GotEmaggedEvent>(OnEmagged);
    }

    private void OnEmagged(Entity<OmuMeleeThrowOnHitComponent> entity, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) ||
            _emag.CheckFlag(entity, EmagType.Interaction) ||
            !TryComp<MeleeThrowOnHitComponent>(entity, out var meleeThrowOnHitComp))
            return;

        args.Handled = true;

        meleeThrowOnHitComp.UnanchorOnHit = entity.Comp.EmagUnanchorOnHit;
        Dirty(entity, meleeThrowOnHitComp);
    }
}

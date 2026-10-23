using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.EntityEffects;

namespace Content.Omu.Server.EntityEffects.Effects;

/// <summary>
/// Reverts a polymorphed entity
/// Created by bookassecond
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class UnpolymorphEntityEffectSystem : EntityEffectSystem<PolymorphedEntityComponent, Shared.EntityEffects.Effects.Unpolymorph>
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    protected override void Effect(Entity<PolymorphedEntityComponent> entity, ref EntityEffectEvent<Shared.EntityEffects.Effects.Unpolymorph> args)
    {
        _polymorph.Revert(entity.Owner);
    }
}

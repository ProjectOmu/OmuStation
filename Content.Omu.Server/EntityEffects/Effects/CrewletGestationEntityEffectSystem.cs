// SPDX-License-Identifier: MIT

using Content.Omu.Server.Crewlet;
using Content.Omu.Shared.EntityEffects.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;

namespace Content.Omu.Server.EntityEffects.Effects;

public sealed partial class CrewletGestationEntityEffectSystem
    : EntityEffectSystem<HumanoidAppearanceComponent, CrewletGestationEntityEffect>
{
    [Dependency] private readonly CrewletGestationSystem _gestation = default!;

    protected override void Effect(Entity<HumanoidAppearanceComponent> entity,
        ref EntityEffectEvent<CrewletGestationEntityEffect> args)
    {
        _gestation.Trigger(entity, args.Effect);
    }
}

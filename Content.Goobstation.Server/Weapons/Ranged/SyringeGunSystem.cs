// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chemistry.Components;
using Content.Shared._Goobstation.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Goobstation.Server.Weapons.Ranged;

/// <summary>
///     System for handling projectiles and altering their properties when fired from a Syringe Gun.
/// </summary>
public sealed class SyringeGunSystem : EntitySystem
{

    public override void Initialize()
    {
        SubscribeLocalEvent<SyringeGunComponent, AmmoShotEvent>(OnFire);
        SubscribeLocalEvent<SyringeGunComponent, AttemptShootEvent>(OnShootAttemot);
    }

    private void OnShootAttemot(Entity<SyringeGunComponent> ent, ref AttemptShootEvent args)
    {
        // Omu start; allows for ballistic guns with a syringe gun component to shoot bullets properly
        // Otherwise, the syringe gun component causes the ammo itself to get thrown and not actually shot
        if (TryComp(ent.Owner, out BallisticAmmoProviderComponent? ammoComp))
        {
            if (ammoComp.Entities.Count > 0 && HasComp<SolutionInjectWhileEmbeddedComponent>(ammoComp.Entities[^1]))
            {
                args.ThrowItems = true;
            }
        }
        else
        {
            args.ThrowItems = true; // Not an Omu line
        } // Omu end; yes this is because of the pneumatic shotgun
    }

    private void OnFire(Entity<SyringeGunComponent> gun, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp(projectile, out SolutionInjectWhileEmbeddedComponent? whileEmbedded))
            {
                whileEmbedded.Injections = null; // uncap the injection maximum
                whileEmbedded.PierceArmorOverride = gun.Comp.PierceArmor;
                whileEmbedded.SpeedMultiplier = gun.Comp.InjectionSpeedMultiplier; // store it in the component to reset it
                whileEmbedded.UpdateInterval /= whileEmbedded.SpeedMultiplier;
            }
            if (TryComp(projectile, out SolutionInjectOnEmbedComponent? onEmbed))
                onEmbed.PierceArmorOverride = gun.Comp.PierceArmor;
        }
    }

}
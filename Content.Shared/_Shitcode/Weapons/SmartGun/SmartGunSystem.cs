// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Goobstation.Weapons.SmartGun;

public sealed class SmartGunSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmartGunComponent, AmmoShotEvent>(OnShot);
    }

    private void OnShot(Entity<SmartGunComponent> ent, ref AmmoShotEvent args)
    {
        var (uid, comp) = ent;

        if (comp.RequiresWield && !(TryComp(uid, out WieldableComponent? wieldable) && wieldable.Wielded))
            return;
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out HomingProjectileComponent? homing) ||
                !TryComp(projectile, out TargetedProjectileComponent? targeted) ||
                targeted.Target is not { } target || target == Transform(uid).ParentUid)
                continue;

            homing.Target = target;
            Dirty(projectile, homing);
        }
    }
}

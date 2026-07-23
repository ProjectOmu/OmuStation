using System.Linq;
using Content.Goobstation.Common.Religion;
using Content.Shared._Goobstation.Heretic.Components;
using Content.Shared._Goobstation.Wizard.TimeStop;
using Content.Shared._Goobstation.Wizard.Traps;
using Content.Shared._Shitcode.Heretic.Systems;
using Content.Shared.Administration;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Heretic;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Omu.Changeling;

public abstract class BerserkAffectedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _weapon = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var rand = new System.Random((int) _timing.CurTick.Value);
        var query = EntityQueryEnumerator<BerserkAffectedComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var affected, out var mobState, out var xform))
        {
            if (_net.IsClient && _player.LocalEntity != uid)
                return;

            var curTime = _timing.CurTime;

            if (curTime < affected.NextAttack)
                return;

            if (!TryComp(uid, out CombatModeComponent? combat))
                return;

            if (_mobState.IsIncapacitated(uid, mobState))
                return;

            if (HasComp<StunnedComponent>(uid) || HasComp<FrozenComponent>(uid) ||
                HasComp<AdminFrozenComponent>(uid) || HasComp<IceCubeComponent>(uid))
                return;

            _gun.TryGetGun(uid, out var gun, out var gunComp);
            _weapon.TryGetWeapon(uid, out var weapon, out var meleeComp);

            float range;
            float attackRate;

            if (gunComp != null)
            {
                if (gunComp.NextFire > curTime)
                    return;

                attackRate = gunComp.FireRate;
                range = 3f;
            }
            else if (meleeComp != null)
            {
                if (meleeComp.NextAttack > curTime)
                    return;

                attackRate = meleeComp.AttackRate;
                range = meleeComp.Range;
            }
            else
                return;

            if (attackRate == 0f)
                return;

            var targets = FindPotentialTargets((uid, xform), affected.ExcludedEntity, range);
            if (targets.Count == 0)
                return;

            affected.NextAttack = curTime + TimeSpan.FromSeconds(1f / attackRate);
            Dirty(uid, affected);

            _combat.SetInCombatMode(uid, true, combat);

            var target = rand.Pick(targets);
            var coords = Transform(target).Coordinates;

            if (gunComp != null)
                _gun.AttemptShoot(uid, gun, gunComp, coords, target);
            else if (meleeComp != null)
                _weapon.AttemptLightAttack(uid, weapon, meleeComp, target);
        }

        if (!_timing.IsFirstTimePredicted)
            return;
    }

    private List<EntityUid> FindPotentialTargets(Entity<TransformComponent> attacker, EntityUid excluded, float range)
    {
        List<EntityUid> result = new();
        var ents = _lookup.GetEntitiesInRange<MobStateComponent>(attacker.Comp.Coordinates, range, LookupFlags.Dynamic);
        foreach (var ent in ents)
        {
            if (ent.Owner == attacker.Owner)
                continue;

            if (_examine.InRangeUnOccluded(attacker, ent, range + 1f))
                result.Add(ent);
        }

        return result;
    }

}

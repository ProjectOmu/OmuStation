using Content.Shared._Goobstation.Wizard.TimeStop;
using Content.Shared._Goobstation.Wizard.Traps;
using Content.Shared.Administration;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Content.Shared.Popups;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Omu.Changeling;

public abstract class BerserkAffectedSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _weapon = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

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

            // This is fucked but i have no idea what is going on here.
            float range = 0;
            float attackRate = 0;
            var isGun = false;
            EntityUid weapon = EntityUid.Invalid;
            MeleeWeaponComponent? meleeComp = null;

            if (_gun.TryGetGun(uid, out var gun))
            {
                if (gun.Comp.NextFire > curTime)
                    return;

                attackRate = gun.Comp.FireRate;
                range = 3f;
                isGun = true;
            }
            else if (_weapon.TryGetWeapon(uid, out weapon, out meleeComp))
            {
                if (meleeComp.NextAttack > curTime)
                    return;

                attackRate = meleeComp.AttackRate;
                range = meleeComp.Range;
            }

            if (attackRate == 0)
                return;

            var targets = FindPotentialTargets((uid, xform), affected.ExcludedEntity, range);
            if (targets.Count == 0)
                return;

            affected.NextAttack = curTime + TimeSpan.FromSeconds(1f / attackRate);
            Dirty(uid, affected);

            _combat.SetInCombatMode(uid, true, combat);

            var target = rand.Pick(targets);
            var coords = Transform(target).Coordinates;

            if (isGun)
                _gun.AttemptShoot(uid, gun, coords, target);
            else if (weapon != EntityUid.Invalid && meleeComp != null)
                _weapon.AttemptLightAttack(uid, weapon, meleeComp, target);

            var message = Loc.GetString(rand.Pick(affected.AngerMessages));
            _popupSystem.PopupEntity(message, uid, uid);
        }
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

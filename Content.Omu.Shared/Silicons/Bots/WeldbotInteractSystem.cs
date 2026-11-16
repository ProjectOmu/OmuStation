// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Shared.Silicon.Bots;
using Content.Goobstation.Shared.Tools;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Interaction;
using Content.Shared.Repairable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Omu.Shared.Silicons.Bots;

public sealed class WeldbotInteractSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RepairableSystem _repairable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeldbotComponent, UserActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<WeldbotComponent, WeldbotWeldDoAfterEvent>(OnWeld);
    }

    private void OnInteract(Entity<WeldbotComponent> ent, ref UserActivateInWorldEvent args)
    {
        if (args.Handled || !CanWeld(ent, args.Target))
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, 3f, new WeldbotWeldDoAfterEvent(),
            args.User, args.Target)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var id))
            return;

        args.Handled = true;

        if (_net.IsServer && TryComp<WeldingSparksComponent>(ent, out var sparks))
            sparks.SpawnedEffects[id.Value] = Spawn(sparks.EffectProto, Transform(args.Target).Coordinates);
    }

    private void OnWeld(Entity<WeldbotComponent> ent, ref WeldbotWeldDoAfterEvent args)
    {
        if (_net.IsServer && TryComp<WeldingSparksComponent>(ent, out var sparks)
            && sparks.SpawnedEffects.Remove(args.DoAfter.Id, out var effect))
            QueueDel(effect);

        if (args.Cancelled || args.Handled || args.Target is not { } target
            || !CanWeld(ent, target) || !TryComp<RepairableComponent>(target, out var repairable))
            return;

        args.Handled = true;

        if (HasComp<EmaggedComponent>(ent))
            _damageable.TryChangeDamage(target, -ent.Comp.DamageAmount, true, false, origin: ent);
        else
            _repairable.ApplyRepairs((target, repairable), ent);

        if (_net.IsServer)
            _audio.PlayPvs(ent.Comp.WeldSound, target);
    }

    private bool CanWeld(Entity<WeldbotComponent> ent, EntityUid target)
    {
        if (!HasComp<RepairableComponent>(target) || !TryComp<DamageableComponent>(target, out var damage))
            return false;

        return HasComp<EmaggedComponent>(ent)
            || ent.Comp.DamageAmount.DamageDict.Keys.Any(key => damage.Damage.DamageDict.GetValueOrDefault(key) > 0);
    }
}

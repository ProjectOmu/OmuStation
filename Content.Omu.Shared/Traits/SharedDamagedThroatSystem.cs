// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Medical;
using Content.Shared.Medical.Stethoscope;
using Content.Shared.Medical.Stethoscope.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Omu.Shared.Traits;

public abstract class SharedDamagedThroatSystem : EntitySystem
{
    private const string StethoscopeDamageType = "Asphyxiation";

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamagedThroatComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<StethoscopeComponent, ComponentStartup>(OnStethoscopeStartup);
        SubscribeLocalEvent<DamagedThroatStethoscopeComponent, StethoscopeDoAfterEvent>(OnStethoscopeDoAfter, before: [typeof(StethoscopeSystem)]);
    }

    private void OnStethoscopeStartup(Entity<StethoscopeComponent> stethoscope, ref ComponentStartup args)
    {
        EnsureComp<DamagedThroatStethoscopeComponent>(stethoscope);
    }

    private void OnExamined(Entity<DamagedThroatComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && !_net.IsClient)
            args.PushMarkup(Loc.GetString("damaged-throat-trait-examined", ("target", Identity.Entity(ent, EntityManager))));
    }

    private void OnStethoscopeDoAfter(Entity<DamagedThroatStethoscopeComponent> stethoscope, ref StethoscopeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!TryComp<DamagedThroatComponent>(target, out var throat)
            || _mobState.IsDead(target)
            || !TryComp<DamageableComponent>(target, out var damageable)
            || !damageable.Damage.DamageDict.TryGetValue(StethoscopeDamageType, out var asphyxiation)
            || asphyxiation >= throat.NormalBreathingThreshold)
            return;

        _popup.PopupPredicted(Loc.GetString(throat.StethoscopeReading), target, args.User);
        args.Handled = true;
        args.Repeat = true;
    }
}


// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Shared._StarLight.Dolls.Components;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._StarLight.Dolls.Systems;

public sealed class ShellSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShellComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShellComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ShellComponent, DamageChangedEvent>(OnDamaged);
    }

    private void OnMapInit(EntityUid uid, ShellComponent component, MapInitEvent args)
    {
        // Give the Doll its manual shell-snapping action.
        _actions.AddAction(
            uid,
            ref component.GenerateShellPieceActionEntity,
            component.GenerateShellPieceAction);

        // Display the Doll's Shell Integrity HUD.
        _alerts.ShowAlert(uid, component.ShellAlert);

        Dirty(uid, component);
    }

    private void OnShutdown(EntityUid uid, ShellComponent component, ComponentShutdown args)
    {
        // Remove the manual snapping action.
        _actions.RemoveAction(uid, component.GenerateShellPieceActionEntity);

        // Remove the Shell Integrity HUD.
        _alerts.ClearAlert(uid, component.ShellAlert);
    }

    private void OnDamaged(EntityUid uid, ShellComponent component, DamageChangedEvent args)
    {
        // Ignore healing and damage caused by the Doll itself.
        if (!args.DamageIncreased || args.Origin == uid)
            return;

        if (args.DamageDelta == null)
            return;

        // Only consider damage belonging to the configured group.
        var group = _prototypes.Index<DamageGroupPrototype>(component.DestroyedBy);

        if (!args.DamageDelta.TryGetDamageInGroup(group, out var damageTaken))
            return;

        // Hits must exceed the shell's hardness.
        if (damageTaken.Float() <= component.Hardness)
            return;

        // Calculate the probability of a shell fragment shattering.
        var chance = component.Stability <= 0f
            ? 1f
            : Math.Clamp(damageTaken.Float() / component.Stability, 0f, 100f) * 0.01f;

        if (!_random.Prob(chance))
            return;

        // Find all attached shell organs.
        var fragments = new List<EntityUid>();

        foreach (var (organId, _) in _body.GetBodyOrgans(uid))
        {
            if (HasComp<OrganShellComponent>(organId))
                fragments.Add(organId);
        }

        if (fragments.Count == 0)
            return;

        // Select a random shell fragment.
        var fragment = _random.Pick(fragments);

        // Remove the fragment using Omu's existing body system.
        if (!_body.TryRemoveOrgan(fragment))
            return;

        // Play the shell-shattering sound.
        _audio.PlayPvs(component.ShellBreakSound, uid);

        // Permanently destroy the fragment.
        QueueDel(fragment);
    }
}

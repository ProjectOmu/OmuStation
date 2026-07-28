// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 ThanosDeGraf <richardgirgindontstop@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Events;
using Content.Server.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared._Shitmed.Body.Organ;
using Content.Server._Shitmed.DelayedDeath;
using Content.Shared.Alert; // Omu
using Content.Shared.Body.Organ; // Omu

namespace Content.Server._Shitmed.Body.Organ;

public sealed class HeartSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly AlertsSystem _alert = default!; // Omu

    private string _faultyHeartAlertId = "FaultyHeart"; // Omu

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeartComponent, OrganAddedToBodyEvent>(HandleAddition);
        SubscribeLocalEvent<HeartComponent, OrganRemovedFromBodyEvent>(HandleRemoval);
        SubscribeLocalEvent<HeartComponent, OrganDisabledEvent>(OnOrganDisabled); // Omu
        SubscribeLocalEvent<HeartComponent, OrganEnabledEvent>(OnOrganEnabled); // Omu
    }

    private void HandleRemoval(EntityUid uid, HeartComponent _, ref OrganRemovedFromBodyEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OldBody))
            return;

        // TODO: Add some form of very violent bleeding effect.
        EnsureComp<DelayedDeathComponent>(args.OldBody);
        _alert.ShowAlert(args.OldBody, _faultyHeartAlertId); // Omu
    }

    private void HandleAddition(EntityUid uid, HeartComponent _, ref OrganAddedToBodyEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.Body))
            return;

        // Omu Edit Start - This a long one
        // Lets make sure the brain is present and the heart inserted is functioning
        // TODO: If ever we have something with multiple brains and braindamage is implemented this needs to be changed.
        if (_bodySystem.TryGetBodyOrganEntityComps<BrainComponent>(args.Body, out var _)
            && TryComp<OrganComponent>(args.Part, out var organ)
            && organ.Enabled)
        {
            RemCompDeferred<DelayedDeathComponent>(args.Body);
            _alert.ClearAlert(args.Body, _faultyHeartAlertId);
        }
    }

    // Heartfailure time
    private void OnOrganDisabled(EntityUid uid, HeartComponent comp, ref OrganDisabledEvent args)
    {
        //We don't care about the alert or dethComp is the heart is lying on the floor.
        if (TryComp<OrganComponent>(uid, out var organ)
            && organ.Body is not null)
        {
            var deth = EnsureComp<DelayedDeathComponent>(organ.Body.Value);
            deth.FromHeartFailure = true;
            _alert.ShowAlert(organ.Body.Value, _faultyHeartAlertId);
        }
    }

    /// <summary>
    /// Deals with the heart failure alert, and if <see cref="DelayedDeathComponent"/> was caused by the heart failure, removing it.
    /// </summary>
    private void OnOrganEnabled(EntityUid uid, HeartComponent comp, ref OrganEnabledEvent args)
    {
        // This probably looks messy
        if (TryComp<OrganComponent>(uid, out var organ)
            && organ.Body is not null
            && TryComp<DelayedDeathComponent>(organ.Body.Value, out var death))
        {
            if (death.FromHeartFailure)
                RemCompDeferred<DelayedDeathComponent>(organ.Body.Value);
            _alert.ClearAlert(organ.Body.Value, _faultyHeartAlertId);
        }
        //Omu Edit End
    }
    // Shitmed-End
}

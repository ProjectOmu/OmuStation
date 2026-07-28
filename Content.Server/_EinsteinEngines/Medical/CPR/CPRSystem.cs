// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.Rotting;
using Content.Server.Body.Components;
using Content.Server.DoAfter;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Nutrition; // Omu

using Content.Shared.Medical.CPR;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Traits.Assorted;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Nutrition.EntitySystems; // Shitmed Change

namespace Content.Server.Medical.CPR;

public sealed class CPRSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly IngestionSystem _ingestionSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly RottingSystem _rottingSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!; // Omu

    public override void Initialize()
    {
        base.Initialize(); SubscribeLocalEvent<CPRTrainingComponent, GetVerbsEvent<InnateVerb>>(AddCPRVerb);
        SubscribeLocalEvent<CPRTrainingComponent, CPRDoAfterEvent>(OnCPRDoAfter);
    }

    private void AddCPRVerb(Entity<CPRTrainingComponent> performer, ref GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !TryComp<MobStateComponent>(args.Target, out var targetState)
            || targetState.CurrentState == MobState.Alive)
            return;

        var target = args.Target;
        InnateVerb verb = new()
        {
            Act = () => { StartCPR(performer, target); },
            Text = Loc.GetString("cpr-verb"),
            Icon = new SpriteSpecifier.Rsi(new("Interface/Alerts/human_alive.rsi"), "health4"),
            Priority = 2
        };

        args.Verbs.Add(verb);
    }

    private void StartCPR(Entity<CPRTrainingComponent> performer, EntityUid target)
    {
        if (HasComp<RottingComponent>(target))
        {
            _popupSystem.PopupEntity(Loc.GetString("cpr-target-rotting", ("entity", target)), performer, performer);
            return;
        }

        if (!HasComp<RespiratorComponent>(target) || !HasComp<RespiratorComponent>(performer))
        {
            _popupSystem.PopupEntity(Loc.GetString("cpr-target-cantbreathe", ("entity", target)), performer, performer);
            return;
        }

        if (_inventory.TryGetSlotEntity(target, "outerClothing", out var outer))
        {
            _popupSystem.PopupEntity(Loc.GetString("cpr-must-remove", ("clothing", outer)), performer, performer);
            return;
        }

        if (!_ingestionSystem.HasMouthAvailable(performer, performer) || !_ingestionSystem.HasMouthAvailable(performer, target)) // Omu, swap parameters to correctly check if target is wearing a blocker
        {
            // Omu, fixes the ingestion blocker text not appearing.
            // Yes, this is shitcode. I am sorry but I don't know how to do this better.

            // first, check if the range requirement is why the interaction failed
            if (!_transform.GetMapCoordinates(performer).InRange(_transform.GetMapCoordinates(target), IngestionSystem.MaxFeedDistance))
            {
                _popupSystem.PopupEntity(Loc.GetString("interaction-system-user-interaction-cannot-reach"), performer, performer);
                return;
            }

            // if not range, then check if it's because someone can't do ingesting
            var attempt = new IngestionAttemptEvent(IngestionSystem.DefaultFlags);
            if(!_ingestionSystem.HasMouthAvailable(performer, performer)) // first check if the performer has a mask
            {
                RaiseLocalEvent(performer, ref attempt);
                if (attempt.Blocker != null)
                    _popupSystem.PopupEntity(Loc.GetString("ingestion-remove-mask", ("entity", attempt.Blocker.Value)), performer, performer);

            } else if (!_ingestionSystem.HasMouthAvailable(performer, target)) // if not, check if the target has a mask; this is to prevent mixing textboxes
            {
                RaiseLocalEvent(target, ref attempt);
                if (attempt.Blocker != null)
                    _popupSystem.PopupEntity(Loc.GetString("ingestion-remove-mask", ("entity", attempt.Blocker.Value)), performer, performer);
            }
            return;
            // Omu end
        }


        _popupSystem.PopupEntity(Loc.GetString("cpr-start-second-person", ("target", target)), target, performer);
        _popupSystem.PopupEntity(Loc.GetString("cpr-start-second-person-patient", ("user", performer)), target, target);

        var doAfterArgs = new DoAfterArgs(
            EntityManager, performer, performer.Comp.DoAfterDuration, new CPRDoAfterEvent(), performer, target,
            performer)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            // Omu, check if you're no longer able to do CPR during the doafter
            // This is using a deprecated feature to avoid dealing with events from other namespaces
            AttemptFrequency = AttemptFrequency.StartAndEnd,
            ExtraCheck = () => !(HasComp<RottingComponent>(target) || // cancels on false, so invert all the previous checks
            !HasComp<RespiratorComponent>(target) || !HasComp<RespiratorComponent>(performer) ||
            _inventory.TryGetSlotEntity(target, "outerClothing", out _) ||
            !_ingestionSystem.HasMouthAvailable(performer, performer) || !_ingestionSystem.HasMouthAvailable(performer, target))
            // Omu end
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);

        var playingStream = _audio.PlayPvs(performer.Comp.CPRSound, performer, AudioParams.Default.WithLoop(true));
        if (!playingStream.HasValue)
            return;

        performer.Comp.CPRPlayingStream = playingStream.Value.Entity;
    }

    private void OnCPRDoAfter(Entity<CPRTrainingComponent> performer, ref CPRDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !args.Target.HasValue)
        {
            performer.Comp.CPRPlayingStream = _audio.Stop(performer.Comp.CPRPlayingStream);
            return;
        }

        if (!performer.Comp.CPRHealing.Empty)
            _damageable.TryChangeDamage(args.Target, performer.Comp.CPRHealing, true, origin: performer, targetPart: TargetBodyPart.All); // Shitmed Change

        if (performer.Comp.RotReductionMultiplier > 0)
            _rottingSystem.ReduceAccumulator(
                (EntityUid)args.Target, performer.Comp.DoAfterDuration * performer.Comp.RotReductionMultiplier);

        if (_robustRandom.Prob(performer.Comp.ResuscitationChance)
            && _mobThreshold.TryGetThresholdForState((EntityUid)args.Target, MobState.Dead, out var threshold)
            && TryComp<DamageableComponent>(args.Target, out var damageableComponent)
            && TryComp<MobStateComponent>(args.Target, out var state)
            && _mobThreshold.CheckVitalDamage(args.Target.Value, damageableComponent) < threshold)// GoobStation
        {//OMU start
            if (TryComp<UnrevivableComponent>(args.Target, out var unrevComp))
            {
                if (!unrevComp!.CPRBlock)
                {
                    _mobStateSystem.ChangeMobState(args.Target.Value, MobState.Critical, state, performer);
                }
            }
            else
            {
                _mobStateSystem.ChangeMobState(args.Target.Value, MobState.Critical, state, performer);
            }
        }//OMU end

        var isAlive = _mobStateSystem.IsAlive(args.Target.Value);
        args.Repeat = !isAlive;
        if (isAlive)
            performer.Comp.CPRPlayingStream = _audio.Stop(performer.Comp.CPRPlayingStream);
    }
}

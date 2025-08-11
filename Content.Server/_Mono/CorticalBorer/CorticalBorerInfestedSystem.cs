// SPDX-FileCopyrightText: 2025 Coenx-flex
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mono.CorticalBorer;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Mono.CorticalBorer;

public sealed class CorticalBorerInfestedSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CorticalBorerSystem _borer = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CorticalBorerInfestedComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, ExaminedEvent>(OnExaminedInfested);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, ComponentShutdown>(OnComponentShutdown);

        SubscribeLocalEvent<CorticalBorerInfestedComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, MobStateChangedEvent>(OnStateChange);
    }

    private void OnInit(Entity<CorticalBorerInfestedComponent> infested, ref MapInitEvent args)
    {
        infested.Comp.ControlContainer = _container.EnsureContainer<Container>(infested, "ControlContainer");
        infested.Comp.InfestationContainer = _container.EnsureContainer<Container>(infested, "InfestationContainer");
    }

    private void OnExaminedInfested(Entity<CorticalBorerInfestedComponent> infected, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange
            || args.Examined != args.Examiner)
            return;

        if (infected.Comp.ControlTimeEnd is not { } cte)
            return;

        var timeRemaining = Math.Floor((cte - _timing.CurTime).TotalSeconds);
        args.PushMarkup(Loc.GetString("cortical-borer-self-examine", ("chempoints", infected.Comp.Borer.Comp.ChemicalPoints)));
        args.PushMarkup(Loc.GetString("infested-control-examined", ("timeremaining", timeRemaining)));
    }

    private void OnStateChange(Entity<CorticalBorerInfestedComponent> infected, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if(infected.Comp.ControlTimeEnd.HasValue)
            _borer.EndControl(infected.Comp.Borer);
    }

    private void OnComponentShutdown(Entity<CorticalBorerInfestedComponent> infected, ref ComponentShutdown args)
    {
        if(infected.Comp.ControlTimeEnd.HasValue)
            _borer.EndControl(infected.Comp.Borer);
    }

    private void OnBodyPartRemoved(Entity<CorticalBorerInfestedComponent> infected, ref BodyPartRemovedEvent args)
    {
        if (TryComp<BodyPartComponent>(args.Part, out var part) &&
            part.PartType == BodyPartType.Head)
        {
            _borer.EndControl(infected.Comp.Borer);
            _borer.TryEjectBorer(infected.Comp.Borer);
        }
    }
}

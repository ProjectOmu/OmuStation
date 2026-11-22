// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Omu.Shared.EntityEffects.Effects;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Crewlet;

public sealed class CrewletGestationSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CrewletSystem _crewlet = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<string> _reagents = new();

    private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery;

    private (EntityUid Target, EntityUid User, GameTick Tick) _injection;

    public override void Initialize()
    {
        base.Initialize();

        _humanoidQuery = GetEntityQuery<HumanoidAppearanceComponent>();

        SubscribeLocalEvent<BloodstreamComponent, TargetBeforeInjectEvent>(OnBeforeInject);
        SubscribeLocalEvent<BloodstreamComponent, SolutionContainerChangedEvent>(OnBloodChanged);
        SubscribeLocalEvent<CrewletGestationComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        CacheReagents();
    }

    public void Trigger(EntityUid host, CrewletGestationEntityEffect effect)
    {
        if (effect.Dose <= FixedPoint2.Zero
            || !_crewlet.CanCreate(host)
            || !TryComp<BloodstreamComponent>(host, out var blood)
            || !_solutions.ResolveSolution(host, blood.BloodSolutionName, ref blood.BloodSolution, out var solution)
            || Present(solution) < effect.Dose)
            return;

        var gestation = EnsureComp<CrewletGestationComponent>(host);
        var owner = DelivererAt(gestation, effect.Dose) ?? host;
        foreach (var reagent in _reagents)
        {
            var quantity = solution.GetTotalPrototypeQuantity(reagent);
            if (quantity > FixedPoint2.Zero)
                _solutions.RemoveReagent(blood.BloodSolution.Value, reagent, quantity);
        }

        gestation.Deliveries.Clear();
        Seed((host, gestation), owner);
    }

    public void Seed(Entity<CrewletGestationComponent?> host, EntityUid owner)
    {
        host.Comp ??= EnsureComp<CrewletGestationComponent>(host);
        host.Comp.Hatches.Add(new CrewletHatch
        {
            OmenAt = _timing.CurTime,
            Owner = owner,
        });
        EnsureComp<ActiveCrewletGestationComponent>(host);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        using var query = EntityQueryEnumerator<ActiveCrewletGestationComponent, CrewletGestationComponent>();
        while (query.MoveNext(out var uid, out var active, out var gestation))
        {
            for (var i = gestation.Hatches.Count - 1; i >= 0; i--)
            {
                if (Advance((uid, gestation), gestation.Hatches[i], now))
                    gestation.Hatches.RemoveAt(i);
            }

            if (gestation.Hatches.Count == 0)
                RemComp(uid, active);
        }
    }

    private bool Advance(Entity<CrewletGestationComponent> host, CrewletHatch hatch, TimeSpan now)
    {
        switch (hatch.Stage)
        {
            case CrewletHatchStage.Gestating when now >= hatch.OmenAt:
                SpawnAttachedTo(host.Comp.GlowEffect, new EntityCoordinates(host, Vector2.Zero));
                hatch.Stage = CrewletHatchStage.Omen;
                return false;
            case CrewletHatchStage.Omen when now >= hatch.OmenAt + host.Comp.WarningDelay:
                Warn(host);
                hatch.Stage = CrewletHatchStage.Warned;
                return false;
            case CrewletHatchStage.Warned when now >= hatch.OmenAt + host.Comp.HatchDelay:
                Hatch(host, hatch.Owner);
                return true;
            default:
                return false;
        }
    }

    private void Warn(Entity<CrewletGestationComponent> host)
    {
        var message = Loc.GetString("crewlet-omen-warning");
        _popup.PopupEntity(message, host, host, PopupType.MediumCaution);

        if (!TryComp<ActorComponent>(host, out var actor))
            return;

        _chat.ChatMessageToOne(ChatChannel.Local,
            message,
            Loc.GetString("crewlet-omen-warning-chat", ("message", message)),
            default,
            false,
            actor.PlayerSession.Channel,
            host.Comp.WarningColor);
    }

    private void Hatch(Entity<CrewletGestationComponent> host, EntityUid recorded)
    {
        _crewlet.Flare(host);

        var owner = TerminatingOrDeleted(recorded) ? host.Owner : recorded;
        if (!_crewlet.TryCreate(host, owner, _transform.GetMapCoordinates(host), out var crewlet))
            return;

        _adminLog.Add(LogType.EntitySpawn,
            LogImpact.Medium,
            $"{ToPrettyString(host):target} grew {ToPrettyString(crewlet.Value):entity}"
            + $" following {ToPrettyString(owner):user}");
    }

    private void OnBeforeInject(Entity<BloodstreamComponent> ent, ref TargetBeforeInjectEvent args)
    {
        if (!args.Cancelled)
            _injection = (ent.Owner, args.EntityUsingInjector, _timing.CurTick);
    }

    private void OnBloodChanged(Entity<BloodstreamComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != ent.Comp.BloodSolutionName || !_humanoidQuery.HasComp(ent))
            return;

        var present = Present(args.Solution);
        TryComp<CrewletGestationComponent>(ent, out var gestation);
        var tracked = FixedPoint2.Zero;
        if (gestation != null)
        {
            foreach (var delivery in gestation.Deliveries)
            {
                tracked += delivery.Amount;
            }
        }

        if (present > tracked)
            Deliver(gestation ?? EnsureComp<CrewletGestationComponent>(ent), Deliverer(ent), present - tracked);
        else if (present < tracked && gestation != null)
            Consume(gestation, tracked - present);
    }

    private FixedPoint2 Present(Solution solution)
    {
        var present = FixedPoint2.Zero;
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (_reagents.Contains(reagent.Prototype))
                present += quantity;
        }

        return present;
    }

    private EntityUid Deliverer(EntityUid host)
    {
        return _injection.Target == host && _injection.Tick == _timing.CurTick ? _injection.User : host;
    }

    private static void Deliver(CrewletGestationComponent gestation, EntityUid deliverer, FixedPoint2 amount)
    {
        var deliveries = gestation.Deliveries;
        if (deliveries.Count > 0 && deliveries[^1].Deliverer == deliverer)
            deliveries[^1] = deliveries[^1] with { Amount = deliveries[^1].Amount + amount };
        else
            deliveries.Add(new CrewletDelivery(deliverer, amount));
    }

    private static void Consume(CrewletGestationComponent gestation, FixedPoint2 amount)
    {
        var deliveries = gestation.Deliveries;
        while (amount > FixedPoint2.Zero && deliveries.Count > 0)
        {
            var first = deliveries[0];
            if (first.Amount > amount)
            {
                deliveries[0] = first with { Amount = first.Amount - amount };
                return;
            }

            amount -= first.Amount;
            deliveries.RemoveAt(0);
        }
    }

    private static EntityUid? DelivererAt(CrewletGestationComponent gestation, FixedPoint2 offset)
    {
        foreach (var delivery in gestation.Deliveries)
        {
            if (offset <= delivery.Amount)
                return delivery.Deliverer;

            offset -= delivery.Amount;
        }

        return null;
    }

    private void OnUnpaused(Entity<CrewletGestationComponent> ent, ref EntityUnpausedEvent args)
    {
        foreach (var hatch in ent.Comp.Hatches)
        {
            hatch.OmenAt += args.PausedTime;
        }
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReagentPrototype>())
            CacheReagents();
    }

    private void CacheReagents()
    {
        _reagents.Clear();
        foreach (var reagent in _proto.EnumeratePrototypes<ReagentPrototype>())
        {
            if (reagent.Metabolisms is not { } metabolisms)
                continue;

            foreach (var entry in metabolisms.Values)
            {
                foreach (var effect in entry.Effects)
                {
                    if (effect is CrewletGestationEntityEffect)
                        _reagents.Add(reagent.ID);
                }
            }
        }
    }
}

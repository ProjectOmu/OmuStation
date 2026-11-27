// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Server.StationEvents.Components;
using Content.Server.Arcade.BlockGame;
using Content.Server.Arcade.SpaceVillain;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.StationEvents.Events;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Shared.Random;

namespace Content.Omu.Server.StationEvents.Events;

public sealed class PolybiusRule : StationEventSystem<PolybiusRuleComponent>
{
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    protected override void Started(EntityUid uid, PolybiusRuleComponent comp, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryGetRandomStation(out var station))
            return;

        var deliverFirst = RobustRandom.Prob(comp.DeliveryChance);
        if (deliverFirst && TryDeliver(station.Value, comp))
            return;

        if (!TrySwapArcade(station.Value, comp) && !deliverFirst)
            TryDeliver(station.Value, comp);
    }

    private bool TryDeliver(EntityUid station, PolybiusRuleComponent comp)
    {
        if (!TryComp<StationCargoOrderDatabaseComponent>(station, out var orders)
            || !TryComp<StationDataComponent>(station, out var stationData))
            return false;

        var product = Loc.GetString(comp.Product);
        var approver = Loc.GetString(comp.Approver);
        var requester = Loc.GetString(comp.Requester);
        var reason = Loc.GetString(comp.Reason);

        var order = new CargoOrderData(CargoSystem.GenerateOrderId(orders), comp.Prototype, product, 0, 1, requester,
            reason, comp.Account, 0);
        order.SetApproverData(approver, requester);
        order.Approved = true;

        if (!TrySendToTelepad((station, stationData), order)
            && !_cargo.AddAndApproveOrder(station,
                comp.Prototype,
                product,
                0,
                1,
                requester,
                reason,
                approver,
                orders,
                comp.Account,
                (station, stationData)))
        {
            _cargo.RemoveOrder(station, comp.Account, orders.NumOrdersCreated, orders);
            return false;
        }

        AnnounceApproval(station, comp, product, approver);
        return true;
    }

    private bool TrySendToTelepad(Entity<StationDataComponent> station, CargoOrderData order)
    {
        using var consoles = EntityQueryEnumerator<CargoOrderConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out var uid, out var console, out var xform))
        {
            if (StationSystem.GetOwningStation(uid, xform) != station.Owner)
                continue;

            var ev = new FulfillCargoOrderEvent(station, order, (uid, console));
            RaiseLocalEvent(ref ev);
            if (ev.Handled)
                return true;
        }

        return false;
    }

    private void AnnounceApproval(EntityUid station, PolybiusRuleComponent comp, string product, string approver)
    {
        var popup = Loc.GetString(comp.Popup, ("approver", approver));
        EntityUid? radioSource = null;

        using var consoles = EntityQueryEnumerator<CargoOrderConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out var console, out _, out var xform))
        {
            if (StationSystem.GetOwningStation(console, xform) != station)
                continue;

            Audio.PlayPvs(comp.ApprovalSound, console);
            _popup.PopupEntity(popup, console, PopupType.Medium);
            radioSource ??= console;
        }

        if (radioSource is not { } source)
            return;

        var message = Loc.GetString("cargo-console-unlock-approved-order-broadcast",
            ("productName", product),
            ("orderAmount", 1),
            ("approver", approver),
            ("cost", Loc.GetString(comp.Cost)));
        _radio.SendRadioMessage(source, message, PrototypeManager.Index(comp.Account).RadioChannel, source,
            escapeMarkup: false);
    }

    private bool TrySwapArcade(EntityUid station, PolybiusRuleComponent comp)
    {
        var arcades = new List<EntityUid>();
        AddStationArcades<BlockGameArcadeComponent>(station, arcades);
        AddStationArcades<SpaceVillainArcadeComponent>(station, arcades);

        if (arcades.Count == 0)
            return false;

        var arcade = RobustRandom.Pick(arcades);
        var xform = Transform(arcade);
        var coordinates = xform.Coordinates;
        var rotation = xform.LocalRotation;

        Del(arcade);
        SpawnAttachedTo(comp.Prototype, coordinates, rotation: rotation);
        return true;
    }

    private void AddStationArcades<T>(EntityUid station, List<EntityUid> arcades) where T : IComponent
    {
        using var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var arcade, out _, out var xform))
        {
            if (StationSystem.GetOwningStation(arcade, xform) != station
                || !_turf.TryGetTileRef(xform.Coordinates, out var tile)
                || _turf.GetContentTileDefinition(tile.Value).IsSubFloor)
                continue;

            arcades.Add(arcade);
        }
    }
}

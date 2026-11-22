// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared.Actions;
using Content.Shared.Foldable;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Omu.Shared.MartialArts;

public sealed class SteelChairSummonSystem : EntitySystem
{
    [Dependency] private readonly FoldableSystem _foldable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SteelChairSummonComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<SteelChairSummonComponent, SummonSteelChairActionEvent>(OnSummon);
    }

    private void OnGetActions(Entity<SteelChairSummonComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands)
            args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnSummon(Entity<SteelChairSummonComponent> ent, ref SummonSteelChairActionEvent args)
    {
        var user = args.Performer;
        if (!TryComp<HandsComponent>(user, out var hands) || hands.Hands.Count == 0)
            return;

        if (ent.Comp.Chair is { } held && Exists(held) && _hands.IsHolding((user, hands), held, out _))
        {
            _popup.PopupClient(Loc.GetString("steel-chair-already-holding"), user, user);
            return;
        }

        if (ent.Comp.Chair is { } nearby
            && Exists(nearby)
            && _transform.InRange(nearby, user, ent.Comp.MinSummonDistance))
        {
            _popup.PopupClient(Loc.GetString("steel-chair-too-close"), user, user);
            return;
        }

        if (!_hands.TryGetEmptyHand((user, hands), out _))
        {
            _popup.PopupClient(Loc.GetString("steel-chair-hands-full"), user, user);
            return;
        }

        args.Handled = true;

        if (!_net.IsServer)
            return;

        if (ent.Comp.Chair is not { } chair || TerminatingOrDeleted(chair))
        {
            chair = Spawn(ent.Comp.ChairPrototype, Transform(user).Coordinates);
            ent.Comp.Chair = chair;
            Dirty(ent);
            RaiseLocalEvent(user, new WrestlingCatchphraseEvent(Loc.GetString(ent.Comp.SummonShout)));
        }

        if (TryComp<FoldableComponent>(chair, out var foldable) && !foldable.IsFolded)
            _foldable.TrySetFolded(chair, foldable, true, user);

        _hands.TryPickupAnyHand(user, chair);
    }
}

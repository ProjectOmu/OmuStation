// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Shared.SlaughterDemon;
using Content.Goobstation.Shared.SlaughterDemon.Systems;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.SlaughterDemon;

public sealed class SlaughterDevourBodySystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly WoundSystem _wound = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, SlaughterDevourAttemptEvent>(OnDevourAttempt, after: [typeof(SlaughterDevourSystem)]);
    }

    private void OnDevourAttempt(Entity<HumanoidAppearanceComponent> ent, ref SlaughterDevourAttemptEvent args)
    {
        if (args.Handled
            || args.Cancelled
            || !TryComp<SlaughterDemonComponent>(args.Devourer, out var demon)
            || demon.IsLaughter
            || !TryComp<BodyComponent>(ent, out var body))
            return;

        var eatHeart = !HasComp<SiliconComponent>(ent)
            && body.Prototype is { } bodyProto
            && _proto.Index(bodyProto).Slots.Values.Any(slot => slot.Organs.ContainsKey("heart"));
        var hearts = _body.GetBodyOrganEntityComps<HeartComponent>((ent, body));
        var limbs = _body.GetBodyChildrenOfType(ent, BodyPartType.Arm, body)
            .Concat(_body.GetBodyChildrenOfType(ent, BodyPartType.Leg, body))
            .ToList();

        if (eatHeart ? hearts.Count == 0 : limbs.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("slaughter-devour-nothing-left"), ent, args.Devourer);
            args.Cancelled = true;
            return;
        }

        args.Handled = true;

        if (_net.IsClient)
            return;

        if (eatHeart)
        {
            foreach (var heart in hearts)
            {
                _body.RemoveOrgan(heart.Owner, heart.Comp2);
                QueueDel(heart.Owner);
            }

            _popup.PopupEntity(Loc.GetString("slaughter-devour-heart-self"), ent, ent, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("slaughter-devour-heart-others", ("demon", args.Devourer), ("target", ent)), ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
            return;
        }

        foreach (var (partId, _) in limbs)
        {
            if (TryComp<WoundableComponent>(partId, out var woundable)
                && woundable.ParentWoundable.HasValue)
                _wound.AmputateWoundable(woundable.ParentWoundable.Value, partId, woundable);
        }

        _popup.PopupEntity(Loc.GetString("slaughter-devour-limbs-self"), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("slaughter-devour-limbs-others", ("demon", args.Devourer), ("target", ent)), ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
    }
}

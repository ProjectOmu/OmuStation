// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Server.StationEvents.Events;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(PolybiusRule))]
public sealed partial class PolybiusRuleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public float DeliveryChance = 0.5f;

    [DataField]
    public ProtoId<CargoAccountPrototype> Account = "Cargo";

    [DataField]
    public LocId Approver = "polybius-delivery-approver";

    [DataField]
    public LocId Requester = "polybius-delivery-requester";

    [DataField]
    public LocId Reason = "polybius-delivery-reason";

    [DataField]
    public LocId Popup = "polybius-delivery-popup";

    [DataField]
    public LocId Cost = "polybius-delivery-cost";

    [DataField]
    public LocId Product = "polybius-delivery-product";

    [DataField]
    public SoundSpecifier ApprovalSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}

// SPDX-License-Identifier: MIT

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Fluids;

[Prototype]
public sealed partial class SmokeBlockedReagentsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<ReagentPrototype>> Reagents = new();
}

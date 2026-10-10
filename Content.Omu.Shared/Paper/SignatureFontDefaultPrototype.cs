// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Paper;

[Prototype]
public sealed partial class SignatureFontDefaultPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Font = string.Empty;

    [DataField]
    public List<ProtoId<SpeciesPrototype>> Species = new();

    [DataField]
    public EntityWhitelist? Whitelist;
}

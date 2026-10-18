// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Fax;

[RegisterComponent]
public sealed partial class FaxCardPrinterComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Card;
}

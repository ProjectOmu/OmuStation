// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Barks;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Speech.Components;

[RegisterComponent]
public sealed partial class BorrowedVoiceComponent : Component
{
    [DataField(required: true)]
    public ProtoId<BarkPrototype> Bark;
}

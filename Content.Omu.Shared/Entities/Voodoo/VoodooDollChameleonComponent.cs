// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Entities.Voodoo;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VoodooDollChameleonComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Parent;

    [DataField, AutoNetworkedField]
    public EntProtoId? Selected;
}

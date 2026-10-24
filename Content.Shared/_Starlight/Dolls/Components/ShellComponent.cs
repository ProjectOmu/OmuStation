// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._StarLight.Dolls.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShellComponent : Component
{
    [DataField]
    public EntProtoId GenerateShellPieceAction = "ActionGenerateShellPiece";

    [DataField, AutoNetworkedField]
    public EntityUid? GenerateShellPieceActionEntity;
}

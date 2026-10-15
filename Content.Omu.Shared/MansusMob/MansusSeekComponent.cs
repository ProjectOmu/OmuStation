using Content.Goobstation.Shared.Overlays;
using Content.Shared.Actions;
using Content.Shared.Polymorph;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Omu.Shared.MansusMob;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MansusSeekComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "ActionMansusSeek";

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionUid;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid TrackedId;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Tracking = false;
}

public sealed partial class ToggleMansusSeek : InstantActionEvent;

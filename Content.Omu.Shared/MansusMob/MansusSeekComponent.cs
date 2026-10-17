using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.MansusMob;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MansusSeekComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "ActionMansusSeek";

    [DataField]
    public int SeekCooldown = 30;

    [DataField]
    public int PassiveDamageCooldown = 5;

    [DataField]
    public int PassiveDamage = 10;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionUid;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid TrackedId;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Tracking = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? NextSeek = null;
}

public sealed partial class ToggleMansusSeek : InstantActionEvent;

public sealed partial class MaidenSpawnEvent : HandledEntityEventArgs
{
    public EntityUid SpawnedEntity;
    public EntityUid TrackedEntity;
}

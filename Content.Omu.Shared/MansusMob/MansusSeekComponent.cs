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

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionUid;

    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid TrackedId;

    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool Tracking;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? NextSeek;
}

public sealed partial class ToggleMansusSeek : InstantActionEvent;

public sealed partial class MaidenSpawnEvent : HandledEntityEventArgs
{
    public EntityUid SpawnedEntity;
    public EntityUid TrackedEntity;
}

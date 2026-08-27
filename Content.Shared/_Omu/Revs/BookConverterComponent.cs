using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Omu.Revs;

[Serializable, NetSerializable]
public sealed partial class RevolutionaryConverterDoAfterEvent : SimpleDoAfterEvent
{
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BookConverterComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public TimeSpan ConversionDuration { get; set; }

    [DataField, AutoNetworkedField]
    public bool Silent { get; set; }

    [DataField, AutoNetworkedField]
    public bool VisibleDoAfter { get; set; }

    //Omu start
    [DataField, AutoNetworkedField]
    public float Amount = -2f;

    [DataField, AutoNetworkedField]
    public float Range = 4f;

    [DataField, AutoNetworkedField]
    public float FocusedMultiplier = 3f;
}

public sealed class MoraleChangedArgs : EntityEventArgs
{
    public EntityUid? User;
    public float Amount;
    public bool? Forced;
}

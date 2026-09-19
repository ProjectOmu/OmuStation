using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
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
    public float Amount = -4f;

    [DataField, AutoNetworkedField]
    public float Range = 4f;

    [DataField, AutoNetworkedField]
    public float FocusedMultiplier = 3f;

    [DataField, AutoNetworkedField]
    public string? TitleBar = "Set Objective";

    [DataField, AutoNetworkedField]
    public string? DescriptorBar = "Set Objective";

    /// <summary>
    /// The battlecry to be said when an entity attacks with this component
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("Objective")]
    [AutoNetworkedField]
    public string? Objective;

    /// <summary>
    /// The maximum amount of characters allowed in a battlecry
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("MaxObjectiveLength")]
    [AutoNetworkedField]
    public int MaxObjectiveLength = 24;

    [DataField] public EntProtoId ConfigureAction = "ActionConfigureMeleeSpeech";

    /// <summary>
    /// The action to open the battlecry UI
    /// </summary>
    [DataField("configureActionEntity")] public EntityUid? ConfigureActionEntity;
}

public sealed class MoraleChangedArgs : EntityEventArgs
{
    public EntityUid? User;
    public float Amount;
    public bool? Forced;
    public string? Objective;
}

[Serializable, NetSerializable]
public sealed class ObjectiveChangedMessage : BoundUserInterfaceMessage
{
    public string Battlecry { get; }
    public ObjectiveChangedMessage(string battlecry)
    {
        Battlecry = battlecry;
    }
}

[Serializable, NetSerializable]
public sealed class RevObjectiveBoundUserInterfaceState : BoundUserInterfaceState
{
    public string CurrentObjective { get; }
    public RevObjectiveBoundUserInterfaceState(string currentObjective)
    {
        CurrentObjective = currentObjective;
    }
}

[Serializable, NetSerializable]
public enum RevObjectiveUiKey : byte
{
    Key,
}

public sealed partial class ObjectiveConfigureActionEvent : InstantActionEvent { }

using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Impstation.Thaven.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedThavenMoodSystem))]
public sealed partial class ThavenMoodsComponent : Component
{
[DataField(required: true)]
    public ProtoId<ThavenMoodConfigPrototype> MoodConfig;

    [DataField, AutoNetworkedField]
    public bool FollowsSharedMoods = true;

    [DataField, AutoNetworkedField]
    public List<ThavenMood> Moods = new();

    [DataField, AutoNetworkedField]
    public SoundSpecifier? MoodsChangedSound = new SoundPathSpecifier("/Audio/_Impstation/Thaven/moods_changed.ogg");

    [DataField(serverOnly: true)]
    public EntityUid? Action;

    /// <summary>
    /// Maximum number of times this entity can be emagged for wildcard moods.
    /// Can be modified by traits.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxWildMoodEmags = 1;

    /// <summary>
    /// How many times this entity has been emagged for wildcard moods so far.
    /// </summary>
    [AutoNetworkedField]
    public int WildMoodEmagCount = 0;
}

public sealed partial class ToggleMoodsScreenEvent : InstantActionEvent
{
}

[NetSerializable, Serializable]
public enum ThavenMoodsUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ThavenMoodsBuiState : BoundUserInterfaceState
{
    public List<ThavenMood> Moods;
    public List<ThavenMood> SharedMoods;

    public ThavenMoodsBuiState(List<ThavenMood> moods, List<ThavenMood> sharedMoods)
    {
        Moods = moods;
        SharedMoods = sharedMoods;
    }
}


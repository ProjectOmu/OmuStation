using Content.Shared._FarHorizons.Vampire.Traits;
using Content.Shared.Alert;
using Content.Shared.Body.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Vampire;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LesserVampireComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public TimeSpan BloodPoolLastUpdated = TimeSpan.Zero;
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public float BloodPoolLastValue = 50;
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public float BloodPoolChange = 0;

    [DataField] public ProtoId<AlertPrototype> BloodPoolAlert = "VampireBloodPool";
    [DataField] public int BloodPoolAlertSegments = 25;
    [DataField] public float BloodPoolMax = 100;
    public float BloodPoolPerLevel => BloodPoolMax / BloodPoolAlertSegments;

    [DataField] public ProtoId<MetabolizerTypePrototype> Metabolizer = "LesserVampire";

    [DataField] public EntProtoId TraitsAction = "ActionOpenVampireTraits";
    [DataField] public Enum TraitsUiKey = VampireTraitsUiKey.Key;

    [DataField] public SoundSpecifier DrinkSound = new SoundPathSpecifier("/Audio/Items/drink.ogg", new AudioParams() { Volume = -1f, MaxDistance = -1f });
    [DataField] public TimeSpan DrinkDelay = TimeSpan.FromSeconds(2.5);
    [DataField] public float DrinkAmount = 3f;

    [DataField] public bool AllowTraitSelection = true;
    [ViewVariables(VVAccess.ReadOnly)] public List<ProtoId<LesserVampireTraitPrototype>>? SelectedTraits;

    [DataField] public TimeSpan BloodPoolRefreshTime = TimeSpan.FromSeconds(1);
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextUpdate = TimeSpan.Zero;
    [DataField] public float HungerDrain = 1.1f;
    [DataField] public float ThristDrain = 1.1f;
}

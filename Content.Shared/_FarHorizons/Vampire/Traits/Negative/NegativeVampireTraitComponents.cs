using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Vampire.Traits.Negative;

// Every trait is capable of having passive bloodpool drain, this is just an "empty" trait with an "empty" system that doesn't do anything else
[RegisterComponent]
public sealed partial class PassiveBloodPoolDrainVampireTraitComponent : LesserVampireTraitComponent;

[RegisterComponent]
public sealed partial class BloodDependencyVampireTraitComponent : LesserVampireTraitComponent
{
    [DataField(required: true)] public DamageSpecifier Damage;
    [DataField] public TimeSpan ImmunityAfterStateChange = TimeSpan.FromSeconds(10);
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan ImmuneUntil;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DefangedVampireTraitComponent : LesserVampireTraitComponent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GourmandVampireTraitComponent : LesserVampireTraitComponent
{
    [DataField, AutoNetworkedField] public bool Inverted;
}

[RegisterComponent]
public sealed partial class UvSensitivityVampireTraitComponent : LesserVampirePassiveTraitComponent
{
    [DataField] public float BloodPoolDrain;
    [ViewVariables(VVAccess.ReadOnly)] public bool CurrentlyDrained = false;
}
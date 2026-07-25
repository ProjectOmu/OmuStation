using Content.Shared.Body.Prototypes;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Vampire.Traits.Positive;

[RegisterComponent]
public sealed partial class SupernaturalStrengthVampireTraitComponent : LesserVampireTraitComponent;

[RegisterComponent]
public sealed partial class HealingBloodVampireTraitComponent : LesserVampireTraitComponent
{
    [DataField(required: true)] public ProtoId<MetabolizerTypePrototype> Metabolizer;
}

[RegisterComponent]
public sealed partial class LanguageAbsorptionVampireTraitComponent : LesserVampireTraitComponent
{
    [DataField] public float Chance = 0.5f;
    [ViewVariables(VVAccess.ReadOnly)] public List<EntityUid> AlreadyChecked = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ExtendableFangsVampireTraitComponent : LesserVampireToggleActionComponent;

[RegisterComponent]
public sealed partial class TransfusionVampireTraitComponent : LesserVampireActionTraitComponent
{
    [DataField] public float IncreaseBloodLevel;
    [DataField] public float DecreaseBloodPool;
    [DataField] public LocId? PopupMessage;
    [DataField] public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(1);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CharmVampireTraitComponent : LesserVampireToggleActionComponent
{
    [DataField] public List<EntityEffect> Effects = new();
    [DataField] public float Range = 1;
    [DataField] public TimeSpan TickRate = TimeSpan.Zero;
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextUpdate = TimeSpan.Zero;
}

[RegisterComponent]
public sealed partial class ConversionVampireTraitComponent : LesserVampireActionTraitComponent
{
    [DataField] public LocId? PopupMessage;
    [DataField] public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(1);
    [DataField] public float DecreaseBloodPool;
    [DataField] public EntProtoId AcceptAction;
    [DataField] public DamageSpecifier ComaHealing = new();
    [DataField] public TimeSpan ConversionTime = TimeSpan.FromSeconds(60);
    [DataField] public List<ProtoId<TagPrototype>> BlacklistTargets = new();
    [DataField] public bool SingleUse;
    [DataField] public bool Convert;
    [DataField] public bool Used;
}

[RegisterComponent]
public sealed partial class VampireConversionCandidateComponent : Component
{
    [DataField] public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
    [ViewVariables(VVAccess.ReadOnly)] public bool DoConvert;
    [ViewVariables(VVAccess.ReadOnly)] public Entity<ConversionVampireTraitComponent>? ConvertedBy;
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextUpdate = TimeSpan.Zero;
    [ViewVariables(VVAccess.ReadOnly)] public bool Accepted;
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan ConvertAt = TimeSpan.Zero;
}
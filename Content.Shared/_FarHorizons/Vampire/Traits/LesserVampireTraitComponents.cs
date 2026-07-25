using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.Vampire.Traits;

public abstract partial class LesserVampireTraitComponent : Component
{
    [DataField] public float PassiveDrain;
}

public abstract partial class LesserVampirePassiveTraitComponent : LesserVampireTraitComponent
{
    [DataField] public TimeSpan TickRate = TimeSpan.Zero;
    [ViewVariables(VVAccess.ReadOnly)] public TimeSpan NextUpdate = TimeSpan.Zero;
}

public abstract partial class LesserVampireActionTraitComponent : LesserVampireTraitComponent
{
    [DataField(required: true)] public EntProtoId Action;
}

public abstract partial class LesserVampireToggleActionComponent : LesserVampireActionTraitComponent
{
    [DataField] public float DrainWhenToggled;

    [DataField, AutoNetworkedField] public bool Toggled;
}
using Content.Shared._FarHorizons.Vampire.Traits;
using Content.Shared.Actions;
using Content.Shared.Body.Prototypes;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Vampire;

[Serializable, NetSerializable]
public sealed partial class LesserVampireDrinkBloodDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class LesserVampireBiteDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

[ByRefEvent]
public record struct MakeVampireOrganEvent(ProtoId<MetabolizerTypePrototype> Metabolizer, bool StomachHandled = false);

[ByRefEvent]
public record struct OutOfBloodPoolEvent;

[ByRefEvent]
public record struct GetVampireBloodPoolChange(float Change = 0);

[ByRefEvent]
public record struct VampireDrinkCheck(Entity<LesserVampireComponent> Vampire, bool Cancelled = false);

[ByRefEvent]
public record struct VampireBiteCheck(Entity<LesserVampireComponent> Vampire, Entity<VampireBiteableComponent> Target, bool Cancelled = false);

[ByRefEvent]
public record struct OnVampireBite(Entity<LesserVampireComponent> Vampire, Entity<VampireBiteableComponent> Target);

[ByRefEvent]
public record struct VampireFangsCheck(bool FangsHidden = false);

public sealed partial class OpenVampireTraitsEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class SubmitVampireTraitSelectionMessage(List<ProtoId<LesserVampireTraitPrototype>> selection) : BoundUserInterfaceMessage
{
    public List<ProtoId<LesserVampireTraitPrototype>> Selection = selection;
}
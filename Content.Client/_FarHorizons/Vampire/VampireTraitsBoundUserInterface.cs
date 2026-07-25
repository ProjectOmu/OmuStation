using Content.Shared._FarHorizons.Vampire;
using Content.Shared._FarHorizons.Vampire.Traits;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.Vampire;

public sealed partial class VampireTraitsBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private VampireTraitsWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<VampireTraitsWindow>();
        _window.SelectTraitsCallback += SubmitTraits;
    }

    private void SubmitTraits(List<ProtoId<LesserVampireTraitPrototype>> traits) =>
        SendMessage(new SubmitVampireTraitSelectionMessage(traits));
}
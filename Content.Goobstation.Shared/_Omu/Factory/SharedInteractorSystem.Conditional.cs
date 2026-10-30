using Content.Goobstation.Shared.SlotMachine.ClawGame;

namespace Content.Goobstation.Shared._Omu.Factory;

public sealed class SharedInteractorConditionalSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClawMachineComponent, InteractorConditionalEvent>(OnClawMachine);
    }

    private void OnClawMachine(EntityUid uid, ClawMachineComponent clawMachine, InteractorConditionalEvent args)
    {
        clawMachine.WeightModifier *= clawMachine.WeightReduction;

        args.Handled = false;
    }
}

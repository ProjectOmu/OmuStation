
using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Server._Omu.Werewolf;

[RegisterComponent]
public sealed partial class DevourHeartsComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Devoured = 0f;
}

public sealed partial class WerewolfObjectiveSystem : EntitySystem
{
    [Dependency] private readonly NumberObjectiveSystem _number = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevourHeartsComponent, ObjectiveGetProgressEvent>(OnAbsorbGetProgress);
    }

    private void OnAbsorbGetProgress(EntityUid uid, DevourHeartsComponent comp, ref ObjectiveGetProgressEvent args)
    {
        var target = _number.GetTarget(uid);
        if (target != 0)
            args.Progress = MathF.Min(comp.Devoured / target, 1f);
        else args.Progress = 1f;
    }
}

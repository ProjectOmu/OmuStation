using Content.Shared._Omu.Entities.Objects.BloodredVim;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Shared.Mech.Components;

public abstract partial class SharedMechPilotSystem : EntitySystem
{

    [Dependency] private readonly ISharedAdminLogManager _admin = default!;
    public override void Initialize()
    {
    SubscribeLocalEvent<MechPilotComponent, BloodredVimBoostActionEvent>(OnPilotBoost);
    }

    private void OnPilotBoost(Entity<MechPilotComponent> ent, ref BloodredVimBoostActionEvent args)
    {
        _admin.Add(LogType.Action, LogImpact.Extreme, $"OnPilotboost activated");
        if (ent.Comp.Mech == null)
            return;

        var mech = ent.Comp.Mech;
        RaiseLocalEvent(mech, args);
    }
}

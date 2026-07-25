using Content.Shared._Omu.Entities.Objects.BloodredVim;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Shared.Mech.Components;

public abstract partial class SharedMechPilotSystem : EntitySystem
{

    [Dependency] private readonly ISharedAdminLogManager _admin = default!;
    public override void Initialize()           //Omu change, enables the mech to listen for the pilot boosting
    {
    SubscribeLocalEvent<MechPilotComponent, BloodredVimBoostActionEvent>(OnPilotBoost);
    }

    private void OnPilotBoost(Entity<MechPilotComponent> ent, ref BloodredVimBoostActionEvent args)     //Omu change, enables the mech to listen for the pilot boosting
    {
        _admin.Add(LogType.Action, LogImpact.Extreme, $"OnPilotboost activated");
        if (ent.Comp.Mech == null)
            return;

        var mech = ent.Comp.Mech;
        RaiseLocalEvent(mech, args);        //Omu send the event to the mech
    }
}

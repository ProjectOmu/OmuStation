using Content.Shared.Construction.Components;
using Content.Shared.Construction;
using Content.Shared.Verbs;

namespace Content.Omu.Shared.Construction;

public sealed class OmuSharedFlatpackSystem : EntitySystem
{
    [Dependency] private readonly SharedFlatpackSystem _sharedFlatSys = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlatpackComponent, GetVerbsEvent<AlternativeVerb>>(OnAddInteractVerb);
    }

    private void OnAddInteractVerb(Entity<FlatpackComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands is null)
            return;
        if (ent.Comp.ToolNeeded) return;
        var user = args.User;
        AlternativeVerb verb = new()
        {
            Message = Loc.GetString("omu-flatpack-alt-click-message"),
            Text = Loc.GetString("omu-flatpack-alt-click-text"),
            Act = () => _sharedFlatSys.UnpackFlatpack(ent, user)
        };

        args.Verbs.Add(verb);
    }
}
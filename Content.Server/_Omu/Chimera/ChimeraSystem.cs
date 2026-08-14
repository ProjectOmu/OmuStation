using Content.Shared.Mind.Components;

// Goobstation
using Content.Server.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Chimera;

public sealed class ChimeraSystem : EntitySystem
{
    [Dependency] private readonly RoleSystem _role = default!;

    private static EntProtoId ChimeraMindRole= "MindRoleChimera";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChimeraComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ChimeraComponent, MindRemovedMessage>(OnMindRemoved);

    }

    private void OnMindAdded(Entity<ChimeraComponent> ent, ref MindAddedMessage args)
    {
        if (!_role.MindHasRole<ChimeraComponent>(args.Mind))
            _role.MindAddRole(args.Mind, ChimeraMindRole, mind: args.Mind.Comp);
    }

    private void OnMindRemoved(Entity<ChimeraComponent> ent, ref MindRemovedMessage args)
    {
        _role.MindRemoveRole<ChimeraComponent>((args.Mind.Owner, args.Mind.Comp));
    }
}

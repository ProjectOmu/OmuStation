using Content.Shared.Mind.Components;
using Content.Server.Roles;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs;
using Content.Server.Mind;

namespace Content.Omu.Server.Chimera;

public sealed class ChimeraSystem : EntitySystem
{
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    private static EntProtoId _chimeraMindRole = "MindRoleChimera";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChimeraComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ChimeraComponent, MindRemovedMessage>(OnMindRemoved);

        SubscribeLocalEvent<ChimeraComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMindAdded(Entity<ChimeraComponent> ent, ref MindAddedMessage args)
    {
        if (!_role.MindHasRole<ChimeraComponent>(args.Mind))
            _role.MindAddRole(args.Mind, _chimeraMindRole, mind: args.Mind.Comp);
    }

    private void OnMindRemoved(Entity<ChimeraComponent> ent, ref MindRemovedMessage args)
    {
        _role.MindRemoveRole<MindRoleChimeraComponent>((args.Mind.Owner, args.Mind.Comp));
    }

    private void OnMobStateChanged(Entity<ChimeraComponent> ent, ref MobStateChangedEvent args)
    {
        if (!_mind.TryGetMind(ent, out var _, out var mind))
            return;

        if (args.NewMobState != MobState.Dead && !_role.MindHasRole<MindRoleChimeraComponent>(ent))
        {
            _role.MindAddRole(ent, _chimeraMindRole, mind: mind);
            return;
        }

        if (args.NewMobState != MobState.Alive && _role.MindHasRole<MindRoleChimeraComponent>(ent))
        {
            _role.MindRemoveRole<MindRoleChimeraComponent>((ent, mind));
        }
    }
}

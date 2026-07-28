using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind.Commands;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Players;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Tag;

// Goobstation
using Content.Shared._Goobstation.Wizard.BindSoul;
using Content.Shared.Mobs.Components;
using Content.Goobstation.Shared.Mind.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Abilities.Mime;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Chimera;

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

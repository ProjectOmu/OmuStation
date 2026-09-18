using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Server.Administration.Logs;
using Content.Shared.Mindshield.Components;
using Content.Server.Popups;
using Content.Shared.NPC.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Server.Mind;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Roles.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Omu.Werewolf;
using Robust.Shared.Player;
using Content.Server.Roles;
using Content.Server.Antag;
using Content.Server.Body.Systems;
using Content.Server.Revolutionary.Components;
using Robust.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Server.Polymorph.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Actions;

namespace Content.Server.Omu.Werewolf;

[RegisterComponent, Access(typeof(WerewolfSystem))]
public sealed partial class WerewolfComponent : Component
{
    public string shapeshiftAction = "ActionWerewolfShift";

}

public sealed class WerewolfSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly PolymorphSystem _poly = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] protected IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    protected BodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WerewolfComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WerewolfComponent, EventWerewolfShiftBasic>(OnShapeshift);
    }

    private void OnStartup(EntityUid uid, WerewolfComponent component, ComponentStartup args)
    {
        //Setup the furry
        _actionsSystem.AddAction(uid, component.shapeshiftAction);
    }

    private void OnShapeshift(EntityUid uid, WerewolfComponent component, EventWerewolfShiftBasic args)
    {
        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        if (!_proto.TryIndex(humanoid.Species, out var speciesPrototype))
            return;


        var entityToGib = Spawn(speciesPrototype.Prototype, Transform(uid).Coordinates);
        _body.GibBody(entityToGib);

        string message = Loc.GetString("WerewolfTransform", ("ent", MetaData(uid).EntityName));

        _popup.PopupEntity(message, uid);

        _poly.PolymorphEntity(args.Performer, args.Form);
    }
}

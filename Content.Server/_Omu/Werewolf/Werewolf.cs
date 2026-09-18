using Content.Server.Popups;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Omu.Werewolf;
using Robust.Shared.Player;
using Content.Server.Body.Systems;
using Content.Server.Polymorph.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Actions;
using Content.Shared.Forensics.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server.Omu.Werewolf;

[RegisterComponent, Access(typeof(WerewolfSystem))]
public sealed partial class WerewolfComponent : Component
{
    [DataField]
    public string ShapeshiftAction = "ActionWerewolfShift";
    [DataField]
    public string RevertAction = "ActionWerewolfRevert";
    [DataField("wolfin")]
    public bool Wolfin { get; set; } = false;

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
    [Dependency] protected BodySystem _body = default!;
    [Dependency] protected SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] protected BloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WerewolfComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WerewolfComponent, EventWerewolfShiftBasic>(OnShapeshift);
        SubscribeLocalEvent<WerewolfComponent, EventWerewolfRevert>(OnRevert);
    }

    private void OnStartup(EntityUid uid, WerewolfComponent component, ComponentStartup args)
    {
        //Setup the furry
        _actionsSystem.AddAction(uid, component.ShapeshiftAction);
        _actionsSystem.AddAction(uid, component.RevertAction);
    }

    private void OnShapeshift(EntityUid uid, WerewolfComponent component, EventWerewolfShiftBasic args)
    {
        if (component.Wolfin)
        {
            _popup.PopupEntity(Loc.GetString("WerewolfAlreadyInForm"), uid, uid, Shared.Popups.PopupType.Medium);
            args.Handled = true;
            return;
        }

        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        if (!_proto.TryIndex(humanoid.Species, out var speciesPrototype))
            return;

        var entityToGib = Spawn(speciesPrototype.Prototype, Transform(uid).Coordinates);

        if (TryComp<DnaComponent>(uid, out var dna))
        {
            if (TryComp<BloodstreamComponent>(entityToGib, out var dummyBlood))
            {
                if (_solutionContainerSystem.ResolveSolution(entityToGib, dummyBlood.BloodSolutionName, ref dummyBlood.BloodSolution, out var bloodSolution))
                {
                    foreach (var reagent in bloodSolution.Contents)
                    {
                        List<ReagentData> reagentData = reagent.Reagent.EnsureReagentData();
                        reagentData.RemoveAll(x => x is DnaData);
                        reagentData.AddRange(_bloodstream.GetEntityBloodData(uid));
                    }
                }
            }
        }

        _body.GibBody(entityToGib);

        string message = Loc.GetString("WerewolfTransform", ("ent", MetaData(uid).EntityName));

        _popup.PopupEntity(message, uid, Shared.Popups.PopupType.LargeCaution);

        var newent = _poly.PolymorphEntity(args.Performer, args.Form);

        if (!TryComp<WerewolfComponent>(newent, out var werewolf))          //Transfer components in polymorph just doesn't work
            return;

        werewolf.Wolfin = true;

    }
    private void OnRevert(EntityUid uid, WerewolfComponent component, EventWerewolfRevert args)
    {
        if (!component.Wolfin)
        {
            _popup.PopupEntity(Loc.GetString("WerewolfAlreadyInForm"), uid, uid, Shared.Popups.PopupType.Medium);
            args.Handled = true;
            return;
        }

        string message = Loc.GetString("WerewolfRevert", ("ent", MetaData(uid).EntityName));

        var newent = _poly.Revert(args.Performer);

        if (newent is not null)
            _popup.PopupEntity(message, newent.Value, Shared.Popups.PopupType.LargeCaution);

        if (!TryComp<WerewolfComponent>(newent, out var werewolf))          //Transfer components in polymorph just doesn't work
            return;

        werewolf.Wolfin = false;
    }
}

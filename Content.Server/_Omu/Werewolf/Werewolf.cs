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
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.DoAfter;
using Content.Shared.Devour.Components;
using Content.Shared.CombatMode.Pacification;

namespace Content.Server.Omu.Werewolf;

[RegisterComponent, Access(typeof(WerewolfSystem))]
public sealed partial class WerewolfComponent : Component
{
    [DataField]
    public bool CanShift = false;

    [DataField]
    public TimeSpan ShiftTime = TimeSpan.FromMinutes(5);
    [DataField]
    public TimeSpan? LastShift;

    [DataField]
    public string ShapeshiftAction = "ActionWerewolfShift";

    [DataField]
    public string RevertAction = "ActionWerewolfRevert";

    [DataField]
    public string DevourAction = "ActionWerewolfDevour";

    [DataField("wolfin")]
    public bool Wolfin { get; set; } = false;

    [DataField]
    public SoundSpecifier? Awoo =
        new SoundPathSpecifier("/Audio/Animals/space_dragon_roar.ogg")
        {
            Params = AudioParams.Default.WithVolume(3f),
        };

    [DataField]
    public int Hearts = 0;

    [DataField]
    public TimeSpan DevourDuration = TimeSpan.FromSeconds(2);
}

public sealed class WerewolfSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly PolymorphSystem _poly = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WerewolfComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WerewolfComponent, EventWerewolfShiftBasic>(OnShapeshift);
        SubscribeLocalEvent<WerewolfComponent, EventWerewolfRevert>(OnRevert);
        SubscribeLocalEvent<WerewolfComponent, EventWerewolfDevour>(OnDevour);
        SubscribeLocalEvent<WerewolfComponent, WerewolfDevourDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<WerewolfComponent, WerewolfShiftArgs>(OnReadyShift);
    }

    public void Update(EntityUid uid, WerewolfComponent component)
    {
        if (component.LastShift is not null && component.CanShift)
        {
            if (component.LastShift + component.ShiftTime >= _gameTiming.CurTime)
            {
                _popup.PopupEntity(Loc.GetString("werewolf-missed-shift"), uid, uid);
                component.CanShift = false;
            }
        }
    }

    private void OnStartup(EntityUid uid, WerewolfComponent component, ComponentStartup args)
    {
        //Setup the furry
        _actionsSystem.AddAction(uid, component.ShapeshiftAction);
        _actionsSystem.AddAction(uid, component.RevertAction);
        _actionsSystem.AddAction(uid, component.DevourAction);
        EnsureComp<PacifiedComponent>(uid);
    }

    private void OnShapeshift(EntityUid uid, WerewolfComponent component, EventWerewolfShiftBasic args)
    {
        if (component.Wolfin)
        {
            _popup.PopupEntity(Loc.GetString("WerewolfAlreadyInForm"), uid, uid, Shared.Popups.PopupType.Medium);
            args.Handled = true;
            return;
        }

        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid) || !_proto.TryIndex(humanoid.Species, out var speciesPrototype))
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

        Roar(uid, component);       //AWOOOO
        component.CanShift = false;

        string message = Loc.GetString("WerewolfTransform", ("ent", MetaData(uid).EntityName));

        _popup.PopupEntity(message, uid, Shared.Popups.PopupType.LargeCaution);

        var newent = _poly.PolymorphEntity(args.Performer, args.Form);

        if (!TryComp<WerewolfComponent>(newent, out var werewolf))          //Transfer components in polymorph just doesn't work
            return;

        werewolf.Wolfin = true;
        werewolf.Hearts = component.Hearts;

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
        werewolf.Hearts = component.Hearts;
        EnsureComp<PacifiedComponent>(newent.Value);
    }

    private void OnDevour(EntityUid uid, WerewolfComponent component, EventWerewolfDevour args)
    {
        var victim = args.Target;

        if (!component.Wolfin)
        {
            _popup.PopupEntity(Loc.GetString("WerewolfNeedsWolfin"), uid, uid, Shared.Popups.PopupType.Medium);
            args.Handled = true;
            return;
        }

        if (_mobState.IsAlive(victim))
        {
            _popup.PopupEntity(Loc.GetString("WerewolfNeedsDead"), uid, uid, Shared.Popups.PopupType.Medium);
            return;
        }

        if (HasComp<WerewolfDevouredComponent>(victim))
        {
            _popup.PopupEntity(Loc.GetString("WerewolfAlreadyConsumed"), uid, uid, Shared.Popups.PopupType.Medium);
            return;
        }

        if (TryComp<BodyComponent>(victim, out var bodyComp))
            if (_body.TryGetBodyOrganEntityComps<HeartComponent>((victim, bodyComp), out var hearts))
            {
                foreach (var heart in hearts)       //This is so stupid
                {
                    QueueDel(heart.Owner);
                    component.Hearts += 1;
                }
                EnsureComp<WerewolfDevouredComponent>(victim);
            }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            uid,
            component.DevourDuration,
            new WerewolfDevourDoAfterEvent(),
            uid,
            args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);

        _popup.PopupEntity(Loc.GetString("WerewolfDevouredAction", ("ent", MetaData(args.Target).EntityName)), uid, uid);
    }

    private void OnDoAfter(EntityUid uid, WerewolfComponent component, WerewolfDevourDoAfterEvent args)
    {
        var victim = args.Target;

        if (victim is null)
            return;

        if (TryComp<BodyComponent>(victim, out var bodyComp))
            if (_body.TryGetBodyOrganEntityComps<HeartComponent>((victim.Value, bodyComp), out var hearts))
            {
                foreach (var heart in hearts)       //This is so stupid
                {
                    QueueDel(heart.Owner);
                    component.Hearts += 1;
                }
                EnsureComp<WerewolfDevouredComponent>(victim.Value);
            }

        _popup.PopupEntity(Loc.GetString("WerewolfDevouredAction", ("ent", MetaData(victim.Value).EntityName)), uid, uid);

        Roar(uid, component);
    }
    private void Roar(EntityUid uid, WerewolfComponent comp)
    {
        if (comp.Awoo != null)
            _audio.PlayPvs(comp.Awoo, uid);
    }

    private void OnReadyShift(EntityUid uid, WerewolfComponent component, WerewolfShiftArgs args)
    {
        component.CanShift = true;
        component.LastShift = _gameTiming.CurTime;

    }
}

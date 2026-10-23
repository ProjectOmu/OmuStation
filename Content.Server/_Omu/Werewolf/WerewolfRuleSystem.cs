using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Omu.Werewolf;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Zombies;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Omu.Werewolf;


public sealed class WerewolfRuleSystem : GameRuleSystem<WerewolfRuleComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;

    private readonly EntProtoId _mindRole = "MindRoleWerewolf";
    private readonly SoundSpecifier _briefingSound = new SoundPathSpecifier("/Audio/Animals/space_dragon_roar.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WerewolfRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
    }
    protected override void Started(EntityUid uid, WerewolfRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.NextShiftTime = component.TimeBetweenShifts;
    }
    protected override void ActiveTick(EntityUid uid, WerewolfRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (_timing.CurTime < component.NextShiftTime)
            return;

        component.NextShiftTime = _timing.CurTime + component.TimeBetweenShifts + component.TimeDelay;

        var ev = new WerewolfShiftArgs();
        ev.Duration = component.TimeDelay;

        var query = EntityQueryEnumerator<WerewolfComponent>();

        while (query.MoveNext(out var entUid, out _))
        {
            RaiseLocalEvent(entUid, ev);
            _popup.PopupEntity(Loc.GetString("werewolf-ready-shift"), entUid, entUid);
        }
    }

    private void OnSelectAntag(EntityUid uid, WerewolfRuleComponent comp, ref AfterAntagEntitySelectedEvent args)
    {
        MakeWerewolf(args.EntityUid);
    }

    public bool MakeWerewolf(EntityUid target)
    {
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        //_role.MindAddRole(mindId, _mindRole, mind, true);

        var briefing = Loc.GetString("werewolf-role-greeting");

        _antag.SendBriefing(target, briefing, Color.MediumPurple, _briefingSound);

        EnsureComp<ZombieImmuneComponent>(target);
        EnsureComp<PacifiedComponent>(target);
        EnsureComp<WerewolfComponent>(target);
        return true;
    }
}


[RegisterComponent, Access(typeof(WerewolfRuleSystem))]
public sealed partial class WerewolfRuleComponent : Component
{
    [DataField]
    public TimeSpan TimeBetweenShifts = TimeSpan.FromMinutes(15);

    [DataField]
    public TimeSpan NextShiftTime;

    /// <summary>
    /// The additional delay added so a werewolf does have to wait the full 15 minutes between turning - since they will have a 5 minute polymorph duration
    /// </summary>
    [DataField]
    public TimeSpan TimeDelay = TimeSpan.FromMinutes(5);
}

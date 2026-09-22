using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Omu.Werewolf;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.Omu.Werewolf;


public sealed class WerewolfRuleSystem : GameRuleSystem<WerewolfRuleComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

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

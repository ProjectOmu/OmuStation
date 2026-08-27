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
using Content.Shared.Revolutionary;
using Robust.Shared.Player;
using Content.Server.Roles;
using Content.Server.Antag;
using Content.Shared._Omu.Revs;

namespace Content.Server._Omu.Revs;

public sealed class MoraleSystem : EntitySystem
{
    public readonly ProtoId<NpcFactionPrototype> RevolutionaryNpcFaction = "Revolutionary";
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedRevolutionarySystem _revolutionarySystem = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MoraleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MoraleComponent, MoraleChangedArgs>(OnChange);
    }

    private void OnStartup(EntityUid uid, MoraleComponent component, ComponentStartup args)
    {
        if (HasComp<MindShieldComponent>(uid))
        {
            component.Mindshielded = true;
            component.MoraleRecovery = 0.4f;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var query = EntityManager.EntityQuery<MoraleComponent>();

        foreach (var morale in query)
        {
            if (TerminatingOrDeleted(morale.Owner))
                continue;

            morale.UpdateAccumulator += frameTime;

            if (morale.UpdateAccumulator >= morale.UpdateTimer)
            {
                morale.UpdateAccumulator -= morale.UpdateTimer;
                Recovery(new Entity<MoraleComponent>(morale.Owner, morale));
            }
        }
    }

    private void Recovery(Entity<MoraleComponent> ent)
    {
        var ev = new MoraleChangedArgs
        {
            Amount = ent.Comp.MoraleRecovery
        };
        RaiseLocalEvent(ent, ev);
    }
    private void OnChange(Entity<MoraleComponent> ent, ref MoraleChangedArgs args)
    {
        if (!_mind.TryGetMind(ent, out _, out _))
            RemComp<MoraleComponent>(ent);

        if (args.Forced == true)
            EnsureComp<MoraleComponent>(ent);

        ent.Comp.MoraleValue += args.Amount;

        var morale = ent.Comp.MoraleValue;

        if (morale <= 0f)
        {
            if (!MakeRev(ent, ref args))
                RemComp<MoraleComponent>(ent);
        }
        if (morale >= 20f)
        {
            RemComp<MoraleComponent>(ent);
        }
    }

    private bool MakeRev(Entity<MoraleComponent> ent, ref MoraleChangedArgs args)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return false;

        if (TryComp<NpcFactionMemberComponent>(ent, out var faction))
            _npcFaction.AddFaction(new Entity<NpcFactionMemberComponent?>(ent, faction), RevolutionaryNpcFaction);

        var revComp = EnsureComp<RevolutionaryComponent>(ent);

        EnsureComp<ShowRevolutionaryIconsComponent>(ent);       //i think all revs being able to see each other is just more fun

        _popup.PopupEntity(Loc.GetString("flash-component-user-head-rev",
        ("victim", Identity.Entity(ent, EntityManager))), ent);

        if (args.User != null)
        {
            _adminLogManager.Add(LogType.Mind,
                LogImpact.Medium,
                $"{ToPrettyString(args.User.Value)} converted {ToPrettyString(ent)} into a Revolutionary");

            if (_mind.TryGetMind(args.User.Value, out var revMindId, out _))
            {
                if (_role.MindHasRole<RevolutionaryRoleComponent>(revMindId, out var role))
                {
                    role.Value.Comp2.ConvertedCount++;
                    Dirty(role.Value.Owner, role.Value.Comp2);
                }
            }
        }

        if (mindId == default || !_role.MindHasRole<RevolutionaryRoleComponent>(mindId))
        {
            _role.MindAddRole(mindId, "MindRoleRevolutionary");
        }

        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
        {
            _antag.SendBriefing(session, Loc.GetString("rev-role-greeting"), Color.Red, revComp.RevStartSound);
        }
        RemComp<MoraleComponent>(ent);
        return true;
    }
}

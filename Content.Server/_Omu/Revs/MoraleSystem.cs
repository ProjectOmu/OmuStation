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
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Revolutionary;
using Robust.Shared.Player;
using Content.Server.Roles;
using Content.Server.Antag;
using Content.Shared._Omu.Revs;
using Content.Server.Revolutionary.Components;
using Robust.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusIcon;
using Content.Goobstation.Shared.CustomFactionIcons;
using Content.Shared.Climbing.Events;

namespace Content.Server._Omu.Revs;

public sealed class MoraleSystem : EntitySystem
{
    public readonly ProtoId<NpcFactionPrototype> RevolutionaryNpcFaction = "Revolutionary";
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRevolutionarySystem _revolutionarySystem = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private const string MoraleNegative = "MoraleNegativeFaction";
    private const string MoraleAverage = "MoraleAverageFaction";
    private const string MoralePositive = "MoralePositiveFaction";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MoraleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MoraleComponent, MoraleChangedArgs>(OnChange);
        SubscribeLocalEvent<MoraleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, MoraleComponent component, ComponentStartup args)
    {
        if (HasComp<CommandStaffComponent>(uid))
        {
            RemComp<MoraleComponent>(uid);
            return;
        }

        if (HasComp<MindShieldComponent>(uid))
        {
            component.Mindshielded = true;
            component.MoraleRecovery = component.MoraleMSRecovery;
        }
        SetMoraleFaction(uid, MoraleAverage);
    }

    private void OnShutdown(EntityUid uid, MoraleComponent component, ComponentShutdown args)
    {
        SetMoraleFaction(uid, null);

        if (TerminatingOrDeleted(uid))
            return;

        if (!HasComp<RevolutionaryComponent>(uid))   //If they are a rev, prevent em from gaining the little icon
        {
            EnsureComp<MoralePassedComponent>(uid, out var comp);       //Handle it here, its so much easier

            if (component.Mindshielded)
            {
                comp.Time = 150f;
            }
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
        if (HasComp<MoralePassedComponent>(ent))
        {
            RemComp<MoraleComponent>(ent);
        }

        if (!_mind.TryGetMind(ent, out _, out _))
        {
            RemComp<MoraleComponent>(ent);
            return;
        }

        if (args.Forced == true)
        {
            EnsureComp<MoraleComponent>(ent);
            if (args.User is not null)
                ForcedMorale(ent.Owner, args.User.Value, args.Amount);
            return;
        }

        if (args.Amount < 0)
        {
            if (ent.Comp.MoraleMsgTicking <= 0)
            {
                var message = Loc.GetString(_random.Pick(ent.Comp.MoraleWarningMsg));
                _popup.PopupEntity(message, ent.Owner, ent.Owner);      //Warning popup
                ent.Comp.MoraleMsgTicking = ent.Comp.MoraleMsgSetpoint;
            }
            ent.Comp.MoraleMsgTicking += args.Amount;
        }

        ent.Comp.MoraleValue += args.Amount;

        var morale = ent.Comp.MoraleValue;
        string faction;

        switch (morale)
        {
            case <= 0f:
                if (!MakeRev(ent, ref args))
                    RemComp<MoraleComponent>(ent);
                return;

            case >= 20f:
                RemComp<MoraleComponent>(ent);
                return;

            case >= 7f and <= 13f:
                faction = MoraleAverage;
                break;

            case < 7f:
                faction = MoraleNegative;
                break;

            case > 13f:
                faction = MoralePositive;
                break;

            default:
                return;
        }
        SetMoraleFaction(ent, faction);
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
            _antag.SendBriefing(session, Loc.GetString("rev-role-greeting-omu"), Color.Red, revComp.RevStartSound); //Omu changed localisation
        }
        RemComp<MoraleComponent>(ent);
        return true;
    }

    public void ForcedMorale(EntityUid ent, EntityUid user, float amount)
    {
        if (!TryComp<MoraleComponent>(ent, out var moraleComponent))
            return;         //Something has gone amiss

        moraleComponent.MoraleValue += amount;

        var morale = moraleComponent.MoraleValue;

        if (morale <= 0f)
        {
            var ev = new MoraleChangedArgs()
            {
                User = user,
                Amount = amount,
                Forced = true,
            };
            if (!MakeRev(new Entity<MoraleComponent>(ent, moraleComponent), ref ev))
                RemComp<MoraleComponent>(ent);
        }
    }

    private void SetMoraleFaction(EntityUid ent, string? newFactionId)
    {
        var userFactionIcons = EnsureComp<CustomFactionIconsComponent>(ent);
        var oldFactions = new[]
        {
            MoraleAverage,
            MoralePositive,
            MoraleNegative
        };

        foreach (var factionId in oldFactions)
        {
            userFactionIcons.FactionIcons.Remove(factionId);
        }

        if (newFactionId is not null)
        {
            userFactionIcons.FactionIcons.Add(newFactionId);
        }

        Dirty(ent, userFactionIcons);
    }
}

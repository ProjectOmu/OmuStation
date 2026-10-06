using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Omu.Server.Chimera.GameTicking.Rules;
using Content.Omu.Shared.Administration;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Heretic;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Administration.Systems;

public sealed partial class OmuAdminVerbSystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;

    private void AddObjectiveWindowVerb(GetVerbsEvent<Verb> args)
    {
        if (!ObjectiveWindowVerbAllowed(args, out var target, out var mind, out _))
            return;

        Verb verb = new()
        {
            Text = Loc.GetString("admin-verb-view-objectives-name"),
            Category = VerbCategory.Admin,
            Icon = new SpriteSpecifier.Rsi(new ResPath("Interface/Actions/actions_borg.rsi"), "state-laws"),
            Act = () =>
            {
                if (!TryGetNetEntity(args.Target, out var netTarget))
                    return;

                RaiseNetworkEvent(new ViewObjectivesWindowMessage(netTarget.Value, MetaData(args.Target).EntityName, mind.Value.Comp.RoleType, mind.Value.Comp.Subtype, GetObjectives(mind.Value)), args.User);
            },
            Impact = LogImpact.Low,
            Message = Loc.GetString("admin-verb-view-objectives-description"),
        };

        args.Verbs.Add(verb);
    }

    private Dictionary<string, List<ObjectiveInfo>> GetObjectives(Entity<MindComponent> mind)
    {
        var result = new Dictionary<string, List<ObjectiveInfo>>();

        foreach (var uid in mind.Comp.Objectives)
        {
            if (_objectives.GetProgress(uid, mind) is not { } progress)
                continue;

            var comp = Comp<ObjectiveComponent>(uid);
            var meta = MetaData(uid);
            var title = meta.EntityName;
            var description = meta.EntityDescription;

            if (comp.Icon == null)
                continue;

            var obj = new ObjectiveInfo(title, description, comp.Icon, progress,comp.ServerCurrency, comp.ServerCurrencyRewardPartial);
            result.GetOrNew(comp.LocIssuer).Add(obj);
        }

        return result;
    }

    public bool ObjectiveWindowVerbAllowed(GetVerbsEvent<Verb> args, [NotNullWhen(true)] out ICommonSession? target, [NotNullWhen(true)] out Entity<MindComponent>? mind, [NotNullWhen(true)] out HereticComponent? heretic)
    {
        target = null;
        mind = null;
        heretic = null;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return false;

        var player = actor.PlayerSession;

        if (!_admin.HasAdminFlag(player, AdminFlags.Fun))
            return false;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return false;

        target = targetActor.PlayerSession;

        if (!_mind.TryGetMind(target.UserId, out mind))
            return false;

        _entityManager.TryGetComponent(mind.Value, out heretic);

        return mind.Value.Comp.Objectives.Count > 0 || heretic is not null;
    }
}

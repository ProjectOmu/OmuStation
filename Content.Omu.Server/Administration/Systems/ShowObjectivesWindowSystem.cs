using System.Linq;
using Content.Omu.Shared.Administration;
using Content.Server._Omu.Heretic.Systems;
using Content.Server.Objectives;
using Content.Shared.Heretic;
using Content.Shared.Mind;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Administration.Systems;

public sealed class ShowObjectivesWindowSystem : EntitySystem
{
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly ListSacrificeTargetsSystem _listSacrificeTargets = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public Dictionary<string, List<ObjectiveInfo>> GetObjectives(Entity<MindComponent> mind)
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

    public List<ViewObjectivesWindowTarget> GetTargets(Entity<MindComponent> mind)
    {
        if (!_listSacrificeTargets.IsHeretic(mind))
            return [];

        var targets = _listSacrificeTargets.GetHereticSacrificeTargets(mind);
        return [.. targets.Select(target => new ViewObjectivesWindowTarget(target.Entity, _listSacrificeTargets.GetHereticTargetName(target.Entity), target.Job))];
    }

    public bool HasObjectivesToShow(Entity<MindComponent> mind)
    {
        _entityManager.TryGetComponent(mind, out HereticComponent? heretic);

        return mind.Comp.Objectives.Count > 0 || heretic is not null;
    }
}

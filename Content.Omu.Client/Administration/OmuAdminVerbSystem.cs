using System.Linq;
using Content.Omu.Client.Administration.UI;
using Content.Omu.Shared.Administration;
using Robust.Shared.Player;

namespace Content.Omu.Client.Administration;

public sealed class OmuAdminVerbSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ViewObjectivesWindowMessage>(OnViewObjectivesWindow);
    }

    private void OnViewObjectivesWindow(ViewObjectivesWindowMessage ev)
    {
        if (!TryGetEntity(ev.Target, out var userUid))
            return;

        if (!_entityManager.TryGetComponent(userUid, out ActorComponent? actor))
            return;

        var objectives = ev.Objectives.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(o => new UI.Components.Objective.AdminObjectiveItem.AdminObjectiveItemModel(o.Icon, o.Title, o.Description, o.Progress)).ToList());
        var targets = ev.Targets.Select(t => new UI.Components.Target.AdminTargetItem.AdminTargetItemModel(t.Target, t.TargetName, t.Job)).ToList();
        var model = new AdminObjectivesWindowModel((userUid.Value, actor), ev.CharacterName, ev.RoleType, ev.RoleTypeSubType, objectives, targets);
        var window = new AdminObjectivesWindow(model);

        window.OpenCentered();
    }
}

using System.Linq;
using Content.Omu.Client.Administration.UI.AdminObjectives;
using Content.Omu.Client.Administration.UI.AdminObjectives.Components;
using Content.Omu.Shared.Administration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Omu.Client.Administration;

public sealed class OmuAdminVerbSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

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

        var objectives = ev.Objectives.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(o => new AdminObjectiveItem.Model(o.Icon, o.Title, o.Description, o.Progress)).ToList());
        var targets = ev.Targets.Select(t => new AdminObjectiveItem.Model(t.Target, t.TargetName, _prototypeManager.Index(t.Job).LocalizedName)).ToList();
        var model = new AdminObjectivesWindowModel((userUid.Value, actor), ev.CharacterName, ev.RoleType, ev.RoleTypeSubType, objectives, targets);
        var window = new AdminObjectivesWindow(model);

        window.OpenCentered();
    }
}

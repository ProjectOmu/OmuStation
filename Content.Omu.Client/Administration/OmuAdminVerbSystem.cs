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
        if (!TryGetEntity(ev.Target, out var target))
            return;

        if (!_entityManager.TryGetComponent(target, out ActorComponent? actor))
            return;

        var model = new AdminObjectivesWindowModel((target.Value, actor), ev.CharacterName, ev.RoleType, ev.RoleTypeSubType, ev.Objectives, [.. ev.Targets.Select(t => new AdminTargetItem.AdminObjectiveTargetModel(t.Target, t.TargetName, t.Job))]);
        var window = new AdminObjectivesWindow(model);

        window.OpenCentered();
    }
}

using Content.Server.Body.Systems;
using Content.Shared._Omu.Weapons.Ranged;
using System.Linq;
using Content.Shared.Body.Components;

namespace Content.Server._Omu.Weapons.Ranged;

public sealed partial class OmuBattleExecuteSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _bodySystem  = default!;

    private ISawmill _sawmill = null!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OmuBattleExecutedEvent>(OnBattleExecuted);

        _sawmill = Logger.GetSawmill("battle-execute");
    }

    private void OnBattleExecuted(OmuBattleExecutedEvent ev)
    {
        var eligibleOrgans = _bodySystem.GetBodyOrgans(ev.Target)
            .Where(o => HasComp<BrainComponent>(o.Id))
            .ToList();

        if (eligibleOrgans.Count <= 0)
            return;

        foreach (var organ in eligibleOrgans)
        {
            _bodySystem.RemoveOrgan(organ.Id, organ.Component);
            _sawmill.Debug($"Removed part {ToPrettyString(organ.Id)} from {ToPrettyString(ev.Target)}");
            QueueDel(organ.Id);
        }
    }
}

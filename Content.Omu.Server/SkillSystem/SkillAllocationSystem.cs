// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Server.SkillSystem.Components;
using Content.Omu.Server.SkillSystem.Events;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;

namespace Content.Omu.Server.SkillSystem;

public sealed class SkillAllocationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Profile.SkillAllocation.Count == 0 || !HasComp<RoundStartSkillGrantingComponent>(ev.Mob))
            return;

        if (CompOrNull<MindContainerComponent>(ev.Mob)?.Mind is not { } mind)
            return;

        var holder = EnsureComp<SkillHolderComponent>(mind);
        foreach (var (skill, amount) in ev.Profile.SkillAllocation)
        {
            holder.SkillDic.TryGetValue(skill, out var current);
            holder.SkillDic[skill] = current + amount;
        }

        var changed = new SkillLevelChengeEvent();
        RaiseLocalEvent(ev.Mob, ref changed);
    }
}

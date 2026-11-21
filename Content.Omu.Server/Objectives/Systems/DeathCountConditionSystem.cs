// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Objectives.Components;

namespace Content.Omu.Server.Objectives.Systems;

public sealed class DeathCountConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindContainerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DeathCountConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnMobStateChanged(EntityUid uid, MindContainerComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !TryComp<MindComponent>(comp.Mind, out var mind))
            return;

        foreach (var objective in mind.Objectives)
        {
            if (TryComp<DeathCountConditionComponent>(objective, out var condition))
                condition.Deaths++;
        }
    }

    private void OnGetProgress(Entity<DeathCountConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = Math.Min(1f, (float) ent.Comp.Deaths / ent.Comp.Required);
    }
}

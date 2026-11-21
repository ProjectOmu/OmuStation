// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Omu.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Shuttles.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Omu.Server.Objectives.Systems;

public sealed class TargetBodyConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TargetBorgedConditionComponent, ObjectiveGetProgressEvent>(OnBorgedGetProgress);
        SubscribeLocalEvent<TargetSpeciesChangedConditionComponent, ObjectiveAfterAssignEvent>(OnSpeciesAfterAssign);
        SubscribeLocalEvent<TargetSpeciesChangedConditionComponent, ObjectiveGetProgressEvent>(OnSpeciesGetProgress);
        SubscribeLocalEvent<TargetOrganConditionComponent, ObjectiveAssignedEvent>(OnOrganAssigned,
            after: [typeof(PickObjectiveTargetSystem)]);
        SubscribeLocalEvent<TargetOrganConditionComponent, ObjectiveGetProgressEvent>(OnOrganGetProgress);
    }

    private void OnBorgedGetProgress(Entity<TargetBorgedConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = HasComp<BorgChassisComponent>(GetTargetMind(ent)?.OwnedEntity) ? 1f : 0f;
    }

    private void OnSpeciesAfterAssign(Entity<TargetSpeciesChangedConditionComponent> ent,
        ref ObjectiveAfterAssignEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(GetTargetMind(ent)?.OwnedEntity, out var humanoid))
            ent.Comp.StartingSpecies = humanoid.Species;
    }

    private void OnSpeciesGetProgress(Entity<TargetSpeciesChangedConditionComponent> ent,
        ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0f;

        if (ent.Comp.StartingSpecies is not { } starting
            || GetTargetMind(ent) is not { OwnedEntity: { } body } mind
            || !TryComp<HumanoidAppearanceComponent>(body, out var humanoid)
            || humanoid.Species == starting)
        {
            return;
        }

        args.Progress = !_mind.IsCharacterDeadIc(mind) && _emergencyShuttle.IsTargetEscaping(body) ? 1f : 0.5f;
    }

    private void OnOrganAssigned(Entity<TargetOrganConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (HasOrgan(GetTargetMind(ent)?.OwnedEntity, ent.Comp.Organ))
            args.Cancelled = true;
    }

    private void OnOrganGetProgress(Entity<TargetOrganConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = HasOrgan(GetTargetMind(ent)?.OwnedEntity, ent.Comp.Organ) ? 1f : 0f;
    }

    private bool HasOrgan(EntityUid? body, string organ)
    {
        return _body.GetBodyOrgans(body).Any(o => Prototype(o.Id)?.ID == organ);
    }

    private MindComponent? GetTargetMind(EntityUid objective)
    {
        return _target.GetTarget(objective, out var target) && TryComp<MindComponent>(target, out var mind)
            ? mind
            : null;
    }
}

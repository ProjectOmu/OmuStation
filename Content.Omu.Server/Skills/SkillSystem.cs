// SPDX-License-Identifier: MIT

using System.Linq;
using System.Text;
using Content.Omu.Shared.Skills.Components;
using Content.Omu.Shared.Skills.Prototypes;
using Content.Omu.Shared.Skills.Systems;
using Content.Server.Antag;
using Content.Shared._Omu.Skills;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Skills;

public sealed class SkillSystem : SharedSkillSystem
{
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;

    private static readonly ProtoId<SkillPrototype> AntagSkill = "Combat";
    private const int AntagSkillBonus = 2;
    private const string HaulingFixture = "skill-hauling";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<SkillBodyComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<SkillBodyComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<SkillGrantingComponent, UseInHandEvent>(OnTrainerUsed);
        SubscribeLocalEvent<SkillHaulingComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<SkillHaulingComponent, PullStoppedMessage>(OnPullStopped);
        SubscribeLocalEvent<SkillBodyComponent, ExaminedEvent>(OnBodyExamined);
    }

    private void OnRoleAdded(RoleAddedEvent ev)
    {
        if (HasComp<SkillHolderComponent>(ev.MindId)
            || !_jobs.MindTryGetJobId(ev.MindId, out var job)
            || job is not { } jobId)
            return;

        var jobSkills = PrototypeManager.EnumeratePrototypes<JobSkillsPrototype>()
            .FirstOrDefault(proto => proto.Jobs.Contains(jobId));
        if (jobSkills == null)
            return;

        var holder = AddComp<SkillHolderComponent>(ev.MindId);
        holder.Skills = new(jobSkills.Skills);
        holder.Departments = new(jobSkills.Departments);
        holder.Commands = new(jobSkills.Commands);

        if (TryComp<HumanoidAppearanceComponent>(ev.Mind.OwnedEntity, out var humanoid)
            && PrototypeManager.EnumeratePrototypes<SkillSpeciesPrototype>()
                .FirstOrDefault(proto => proto.Species.Contains(humanoid.Species)) is { } species)
        {
            foreach (var skill in PrototypeManager.EnumeratePrototypes<SkillPrototype>())
            {
                var adjustment = jobSkills.Specialties.Contains(skill.ID) ? species.Specialty : species.Other;
                holder.Skills[skill.ID] = holder.Skills.GetValueOrDefault(skill.ID) + adjustment;
            }
        }

        UpdateSkills((ev.MindId, holder));
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Profile.SkillAllocation.Count == 0 || !MindSystem.TryGetMind(ev.Mob, out var mind, out _))
            return;

        var holder = EnsureComp<SkillHolderComponent>(mind);
        foreach (var (skill, points) in ev.Profile.SkillAllocation)
        {
            holder.Skills[skill] = holder.Skills.GetValueOrDefault(skill) + points;
        }

        UpdateSkills((mind, holder));
    }

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (!MindSystem.TryGetMind(args.EntityUid, out var mind, out _))
            return;

        var holder = EnsureComp<SkillHolderComponent>(mind);
        holder.Skills[AntagSkill] = holder.Skills.GetValueOrDefault(AntagSkill) + AntagSkillBonus;
        UpdateSkills((mind, holder));
    }

    private void OnMindAdded(Entity<SkillBodyComponent> ent, ref MindAddedMessage args)
    {
        RefreshBody(ent, args.Mind.Owner);
    }

    private void OnMindRemoved(Entity<SkillBodyComponent> ent, ref MindRemovedMessage args)
    {
        RemComp<SkillCommandComponent>(ent);
        SetStaminaMultiplier(ent, 1f);
    }

    private void OnTrainerUsed(Entity<SkillGrantingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !MindSystem.TryGetMind(args.User, out var mind, out _))
            return;

        var holder = EnsureComp<SkillHolderComponent>(mind);
        var learned = false;
        foreach (var (skill, level) in ent.Comp.Skills)
        {
            if (holder.Skills.GetValueOrDefault(skill) >= level)
                continue;

            holder.Skills[skill] = level;
            learned = true;
        }

        if (!learned)
            return;

        args.Handled = true;
        UpdateSkills((mind, holder));
        if (ent.Comp.DestroyAfterUse)
            QueueDel(ent);
    }

    private void OnPullStarted(Entity<SkillHaulingComponent> ent, ref PullStartedMessage args)
    {
        if (args.PulledUid != ent.Owner
            || !MindSystem.TryGetMind(args.PullerUid, out var mind, out _)
            || !TryComp<FixturesComponent>(ent, out var fixtures)
            || fixtures.Fixtures.Count == 0)
            return;

        var density = GetModifier(ent.Comp.Modifiers, GetLevel(mind, ent.Comp.Skills))
            / GetCommandBonus(args.PullerUid, mind);
        _fixtures.TryCreateFixture(ent,
            fixtures.Fixtures.Values.First().Shape,
            HaulingFixture,
            density,
            manager: fixtures);
    }

    private void OnPullStopped(Entity<SkillHaulingComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid == ent.Owner)
            _fixtures.DestroyFixture(ent, HaulingFixture);
    }

    private void OnBodyExamined(Entity<SkillBodyComponent> ent, ref ExaminedEvent args)
    {
        if (args.Examiner != ent.Owner
            && !HasComp<SkillShowoffComponent>(ent)
            && !HasComp<SkillExaminerComponent>(args.Examiner))
            return;

        if (!MindSystem.TryGetMind(ent, out var mind, out _) || !HasComp<SkillHolderComponent>(mind))
            return;

        var text = new StringBuilder(Loc.GetString("skill-examine-header"));
        foreach (var skill in PrototypeManager.EnumeratePrototypes<SkillPrototype>().OrderBy(skill => skill.ID))
        {
            text.Append('\n');
            text.Append(Loc.GetString("skill-examine-entry",
                ("skill", Loc.GetString(skill.Name)),
                ("level", GetLevel(mind, [skill.ID]))));
        }

        args.PushMarkup(text.ToString());
    }

    private void UpdateSkills(Entity<SkillHolderComponent> holder)
    {
        Dirty(holder);
        if (CompOrNull<MindComponent>(holder)?.OwnedEntity is { } body
            && TryComp<SkillBodyComponent>(body, out var skillBody))
            RefreshBody((body, skillBody), holder);
    }

    private void RefreshBody(Entity<SkillBodyComponent> body, EntityUid mind)
    {
        SetStaminaMultiplier(body, GetModifier(body.Comp.StaminaModifiers, GetLevel(mind, body.Comp.StaminaSkills)));

        var command = GetModifier(body.Comp.CommandModifiers, GetLevel(mind, body.Comp.CommandSkills));
        if (command <= 1f || CompOrNull<SkillHolderComponent>(mind)?.Commands is not { Count: > 0 } commands)
        {
            RemComp<SkillCommandComponent>(body);
            return;
        }

        var aura = EnsureComp<SkillCommandComponent>(body);
        aura.Departments = new(commands);
        aura.Multiplier = command;
        aura.Range = body.Comp.CommandRange;
        Dirty(body, aura);
    }

    private void SetStaminaMultiplier(Entity<SkillBodyComponent> body, float multiplier)
    {
        if (!TryComp<StaminaComponent>(body, out var stamina) || multiplier == body.Comp.StaminaMultiplier)
            return;

        stamina.Decay *= multiplier / body.Comp.StaminaMultiplier;
        body.Comp.StaminaMultiplier = multiplier;
        Dirty(body.Owner, stamina);
    }
}

// SPDX-License-Identifier: MIT

using System.Linq;
using System.Text;
using Content.Omu.Common.CCVar;
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
using Robust.Shared.Configuration;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Skills;

public sealed class SkillSystem : SharedSkillSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;

    private bool _skill_system_effect_toggle = true;
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

        Subs.CVar(_cfg, OmuCVars.SkillSystemEffectToggle, value => _skill_system_effect_toggle = false, true);
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

        EnsureSetSkillBatch(ev.MindId, new(jobSkills.Skills));
        var holder = EnsureComp<SkillHolderComponent>(ev.MindId);//we get the comp we just added
        holder.Departments = new(jobSkills.Departments);
        holder.Commands = new(jobSkills.Commands);

        if (TryComp<HumanoidAppearanceComponent>(ev.Mind.OwnedEntity, out var humanoid)
            && PrototypeManager.EnumeratePrototypes<SkillSpeciesPrototype>()
                .FirstOrDefault(proto => proto.Species.Contains(humanoid.Species)) is { } species)
        {
            foreach (var skill in PrototypeManager.EnumeratePrototypes<SkillPrototype>())
            {
                var adjustment = jobSkills.Specialties.Contains(skill.ID) ? species.Specialty : species.Other;
                TryAjustSkill(ev.MindId, skill, adjustment, batchOverride: true);//active batch overrride as we call the Update Skill functon ourselfs
            }
        }

        UpdateSkills((ev.MindId, holder));
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Profile.SkillAllocation.Count == 0 || !MindSystem.TryGetMind(ev.Mob, out var mind, out _))
            return;

        foreach (var (skill, points) in ev.Profile.SkillAllocation)
        {
            EnsureAjustSkill(mind, skill, points, batchOverride: true);
        }
        var holder = EnsureComp<SkillHolderComponent>(mind);
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
        //we dont want entities that are not meant to have the skill system to be able to obtain skills
        if (!TryComp<SkillHolderComponent>(mind, out var holder)) return;
        var learned = false;
        TrySetSkillBatch(mind, ent.Comp.Skills, out var results, onlyIncrease: ent.Comp.OnlyIncreaseSkillLevel);
        foreach (var result in results)
        {
            if (result.Value) learned = true;
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
    public void EnsureAjustSkillBatch(EntityUid mindId, Dictionary<ProtoId<SkillPrototype>, int> valuePairs)
    {
        if (!_skill_system_effect_toggle) return;
        var holder = AddComp<SkillHolderComponent>(mindId);
        foreach (var iterator in valuePairs)
        {
            TryAjustSkill(mindId, iterator.Key, iterator.Value, batchOverride: true);
        }
        UpdateSkills(new(mindId, holder));
    }
    public bool TryAjustSkillBatch(EntityUid mindId, Dictionary<ProtoId<SkillPrototype>, int> valuePairs, out Dictionary<ProtoId<SkillPrototype>, bool> results)
    {
        results = new();
        if (!_skill_system_effect_toggle) return false;
        if (!TryComp<SkillHolderComponent>(mindId, out var holder)) return false;
        foreach (var iterator in valuePairs)
        {
            results[iterator.Key] = TryAjustSkill(mindId, iterator.Key, iterator.Value, batchOverride: true);
        }
        UpdateSkills(new(mindId, holder));
        return true;
    }
    /// <summary>
    /// Function that ensures that the mind of the target will recive the required components
    /// </summary>
    public void EnsureAjustSkill(EntityUid mindId, ProtoId<SkillPrototype> skill, int adjustment, bool batchOverride = false)
    {
        if (!_skill_system_effect_toggle) return;
        var holder = EnsureComp<SkillHolderComponent>(mindId);
        TryAjustSkill(mindId, skill, adjustment, batchOverride: batchOverride);
    }
    /// <summary>
    /// Tries to ajust the skill level fails if the target does not have a SkillHolder component
    /// </summary>
    /// <returns>if skills where added or not</returns>
    public bool TryAjustSkill(EntityUid mindId, ProtoId<SkillPrototype> skill, int adjustment, bool batchOverride = false)
    {
        if (!_skill_system_effect_toggle) return false;
        if (!TryComp<SkillHolderComponent>(mindId, out var holder)) return false;
        holder.Skills[skill] = holder.Skills.GetValueOrDefault(skill) + adjustment;
        if (!batchOverride) UpdateSkills(new(mindId, holder));
        return true;
    }
    public void EnsureSetSkillBatch(EntityUid mindId, Dictionary<ProtoId<SkillPrototype>, int> valuePairs, bool onlyIncrease = false)
    {
        var holder = AddComp<SkillHolderComponent>(mindId);
        foreach (var iterator in valuePairs)
        {
            TrySetSkill(mindId, iterator.Key, iterator.Value, onlyIncrease: onlyIncrease, batchOverride: true);
        }
        UpdateSkills(new(mindId, holder));
    }
    public bool TrySetSkillBatch(EntityUid mindId, Dictionary<ProtoId<SkillPrototype>, int> valuePairs, out Dictionary<ProtoId<SkillPrototype>, bool> results, bool onlyIncrease = false)
    {
        results = new();
        if (!_skill_system_effect_toggle) return false;
        if (!TryComp<SkillHolderComponent>(mindId, out var holder)) return false;
        foreach (var iterator in valuePairs)
        {
            results[iterator.Key] = TrySetSkill(mindId, iterator.Key, iterator.Value, batchOverride: true, onlyIncrease: onlyIncrease);
        }
        UpdateSkills(new(mindId, holder));
        return true;
    }
    public void EnsureSetSkill(EntityUid mindId, ProtoId<SkillPrototype> skill, int value, bool onlyIncrease = false)
    {
        if (!_skill_system_effect_toggle) return;
        var holder = AddComp<SkillHolderComponent>(mindId);
        TrySetSkill(mindId, skill, value, onlyIncrease: onlyIncrease);
    }
    /// <summary>
    /// Attempts to set a skill level, returns false if either the target does not have a skill holder component OR if the skill level is already at that level
    /// </summary>
    /// <param name="mindId"></param>
    /// <param name="onlyIncrease">Will not ajust the value of the Skill level in case it is lower if set to true</param>
    /// <returns></returns>
    public bool TrySetSkill(EntityUid mindId, ProtoId<SkillPrototype> skill, int value, bool onlyIncrease = false, bool batchOverride = false)
    {
        if (!_skill_system_effect_toggle) return false;
        if (!TryComp<SkillHolderComponent>(mindId, out var holder)) return false;
        if (onlyIncrease && holder.Skills.GetValueOrDefault(skill) >= value) return false;
        if (holder.Skills.GetValueOrDefault(skill) == value) return false;
        holder.Skills[skill] = value;
        if (!batchOverride) UpdateSkills(new(mindId, holder));
        return true;
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
        if (!TryComp<StaminaComponent>(body, out var stamina) || multiplier == body.Comp.StaminaMultiplier || !body.Comp.EffectsStamina)
            return;

        stamina.Decay *= multiplier / body.Comp.StaminaMultiplier;
        body.Comp.StaminaMultiplier = multiplier;
        Dirty(body.Owner, stamina);
    }
}

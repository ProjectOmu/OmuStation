// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Omu.Common.DoAfter;
using Content.Omu.Shared.Skills.Components;
using Content.Shared._Omu.Skills;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Skills.Systems;

public abstract class SharedSkillSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly SharedMindSystem MindSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillRequirementComponent, DoAfterUsedDelayEvent>(OnUsedDelay);
        SubscribeLocalEvent<SkillRequirementComponent, ExaminedEvent>(OnRequirementExamined);
        SubscribeLocalEvent<SkillSpreadComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnUsedDelay(Entity<SkillRequirementComponent> ent, ref DoAfterUsedDelayEvent args)
    {
        if (!_hands.IsHolding(args.User, ent.Owner) || !MindSystem.TryGetMind(args.User, out var mind, out _))
            return;

        if (ent.Comp.RequiredSkill is { } required && GetLevel(mind, [required]) < ent.Comp.RequiredLevel)
        {
            args.Cancelled = true;
            return;
        }

        args.Multiplier /= GetSpeed(args.User, mind, ent.Comp);
    }

    private void OnRequirementExamined(Entity<SkillRequirementComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.HideExamine || !MindSystem.TryGetMind(args.Examiner, out var mind, out _))
            return;

        if (ent.Comp.RequiredSkill is { } required && PrototypeManager.TryIndex(required, out var skill))
        {
            args.PushMarkup(Loc.GetString("skill-requirement-examine-required",
                ("skill", Loc.GetString(skill.Name)),
                ("level", ent.Comp.RequiredLevel)));
        }

        args.PushMarkup(Loc.GetString("skill-requirement-examine-speed",
            ("speed", Math.Round(GetSpeed(args.Examiner, mind, ent.Comp), 2))));
    }

    private void OnGunRefreshModifiers(Entity<SkillSpreadComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (args.Gun.Comp.Holder is not { } holder || !MindSystem.TryGetMind(holder, out var mind, out _))
            return;

        var spread = GetModifier(ent.Comp.Modifiers, GetLevel(mind, ent.Comp.Skills))
            / GetCommandBonus(holder, mind);
        args.MinAngle *= spread;
        args.MaxAngle *= spread;
    }

    private float GetSpeed(EntityUid user, EntityUid mind, SkillRequirementComponent requirement)
    {
        return GetModifier(requirement.Modifiers, GetLevel(mind, requirement.Skills))
            * GetCommandBonus(user, mind);
    }

    protected int GetLevel(EntityUid mind, IEnumerable<ProtoId<SkillPrototype>> skills)
    {
        if (!TryComp<SkillHolderComponent>(mind, out var holder))
            return 0;

        var level = int.MinValue;
        foreach (var skill in skills)
        {
            level = Math.Max(level, holder.Skills.GetValueOrDefault(skill));
        }

        return level == int.MinValue ? 0 : level;
    }

    protected static float GetModifier(SortedDictionary<int, float> modifiers, int level)
    {
        if (modifiers.Count == 0)
            return 1f;

        var modifier = modifiers.Values.First();
        foreach (var (threshold, value) in modifiers)
        {
            if (threshold > level)
                break;

            modifier = value;
        }

        return modifier;
    }

    protected float GetCommandBonus(EntityUid user, EntityUid mind)
    {
        if (!TryComp<SkillHolderComponent>(mind, out var holder) || holder.Departments.Count == 0)
            return 1f;

        var coordinates = Transform(user).Coordinates;
        var bonus = 1f;
        using var query = EntityQueryEnumerator<SkillCommandComponent, TransformComponent>();
        while (query.MoveNext(out var command, out var xform))
        {
            if (command.Multiplier > bonus
                && command.Departments.Overlaps(holder.Departments)
                && _transform.InRange(coordinates, xform.Coordinates, command.Range))
                bonus = command.Multiplier;
        }

        return bonus;
    }
}

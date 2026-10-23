using Content.Omu.Server.SkillSystem.Components;
using Content.Omu.Server.SkillSystem.Events;
using Content.Omu.Common.SkillSystem;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;

namespace Content.Omu.Server.SkillSystem;

public sealed partial class OmuSkillSystem : CommenOmuSkillSystem
{
    public void RoundStartSkillGrantingStartup(Entity<RoundStartSkillGrantingComponent> ent, ref ComponentStartup args)
    {
        GrantRoundStartSkills(ent);
    }
    public void RoundStartSkillGrantingStartup(Entity<RoundStartSkillGrantingComponent> ent, ref MindAddedMessage args)
    {
        GrantRoundStartSkills(ent);
    }
    public void GrantRoundStartSkills(Entity<RoundStartSkillGrantingComponent> ent)
    {
        EnsureComp<SkillHolderBodyComponent>(ent);
        if (!TryComp<MindContainerComponent>(ent, out var mindContainer)) return;
        if (!mindContainer.HasMind) return;
        var skillComp = EnsureComp<SkillHolderComponent>((EntityUid) mindContainer.Mind);
        foreach (var iterator in ent.Comp.SkillDic.Keys)
        {
            skillComp.SkillDic.Add(iterator, ent.Comp.SkillDic[iterator]);
        }
        skillComp.Department = ent.Comp.Department;
        if (TryComp<SpeciesBasedSkillAdditionComponent>(ent, out var speciesBased))
        {
            CheckSpeciesBasedAdditions(ent, skillComp, speciesBased);
        }
        SkillLevelChengeEvent ev = new();
        RaiseLocalEvent(ent, ref ev);
    }
    public void CheckSpeciesBasedAdditions(EntityUid ent, SkillHolderComponent skillComp, SpeciesBasedSkillAdditionComponent speciesBasedComp)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var appearanceComp)) return;
        foreach (var iterator in speciesBasedComp.SkillDic.Keys)
        {
            if (appearanceComp.Species != iterator) continue;
            foreach (var iteratorInternal in speciesBasedComp.SkillDic[iterator].Keys)
            {
                if (skillComp.SkillDic.ContainsKey(iteratorInternal)) skillComp.SkillDic[iteratorInternal] += speciesBasedComp.SkillDic[iterator][iteratorInternal];
                else
                {
                    skillComp.SkillDic.Add(iteratorInternal, speciesBasedComp.SkillDic[iterator][iteratorInternal]);
                }
            }
        }
    }
    public void AntagStartSkillGrantingStartup(Entity<AntagSkillGrantingComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<MindContainerComponent>(ent, out var mindContainer)) return;
        if (!mindContainer.HasMind) return;
        var skillComp = EnsureComp<SkillHolderComponent>((EntityUid) mindContainer.Mind);
        foreach (var iterator in ent.Comp.SkillDic.Keys)
        {
            if (skillComp.SkillDic.ContainsKey(iterator))
            {
                if (skillComp.SkillDic[iterator] < ent.Comp.SkillDic[iterator])
                {
                    skillComp.SkillDic[iterator] = ent.Comp.SkillDic[iterator];
                }
            }
            else
            {
                skillComp.SkillDic.Add(iterator, ent.Comp.SkillDic[iterator]);
            }
        }
        if (TryComp<SpeciesBasedSkillAdditionComponent>(ent, out var speciesBased))
        {
            CheckSpeciesBasedAdditions(ent, skillComp, speciesBased);
        }
        SkillLevelChengeEvent ev = new();
        RaiseLocalEvent(ent, ref ev);
    }
}

using Content.Omu.Server.SkillSystem.Components;
using Content.Omu.Server.SkillSystem.Events;
using Content.Omu.Common.SkillSystem;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;

namespace Content.Omu.Server.SkillSystem;

public sealed partial class OmuSkillSystem : CommenOmuSkillSystem
{
    public void CommandCompStartup(Entity<CommandSkillComponent> ent, ref ComponentStartup args)
    {
        CommandSkillStart(ent);
    }
    public void CommandCompStartup(Entity<CommandSkillComponent> ent, ref MindAddedMessage args)
    {
        CommandSkillStart(ent);
    }
    public void CommandSkillStart(Entity<CommandSkillComponent> ent)
    {
        ent.Comp.ParsedSpeedModifier = new();
        if (TryComp<MindComponent>(ent, out var _)) return;//if we are on the mind we just leave
        foreach (var iterator in ent.Comp.SpeedModifier.Keys)
        {
            if (int.TryParse(iterator, out var parsedInt))
            {
                if (ent.Comp.SpeedModifier[iterator] == 0)
                {
                    Log.Error("WARNING SKILL COMPONENT SPEED MODIFER WAS EQUAL TO ZERO not permitted.");
                    RemComp<SkillRequermentComponent>(ent);
                    return;
                }
                ent.Comp.ParsedSpeedModifier.Add(parsedInt, ent.Comp.SpeedModifier[iterator]);
            }
        }
        if (!TryComp<MindContainerComponent>(ent, out var mindContainer)) return;
        if (!mindContainer.HasMind) return;
        AddCommandSkillToMind((EntityUid) mindContainer.Mind, ent.Comp);
    }
    public void AddCommandSkillToMind(EntityUid mindUid, CommandSkillComponent bodyCommandSkillComp)//ensure the mind has this as this is what we will be using primarly
    {
        var commandSkillComp = EnsureComp<CommandSkillComponent>(mindUid);
        commandSkillComp.Range = bodyCommandSkillComp.Range;
        commandSkillComp.EffectedDepartments = bodyCommandSkillComp.EffectedDepartments;
        commandSkillComp.ParsedSpeedModifier = bodyCommandSkillComp.ParsedSpeedModifier;
    }
    public void AddCommandSkillToMind(Entity<MindContainerComponent> ent, ref SkillLevelChengeEvent args)
    {
        if (!ent.Comp.HasMind) return;
        var mindUid = (EntityUid) ent.Comp.Mind;
        if (!TryComp<SkillHolderComponent>(mindUid, out var skillHolderComp)) return;
        if (skillHolderComp.SkillDic.ContainsKey("Command"))
        {
            if (TryComp<CommandSkillComponent>(mindUid, out var _)) return;
            var commandSkillComp = EnsureComp<CommandSkillComponent>(mindUid);
            commandSkillComp.ParsedSpeedModifier.Add(-1, 0.5f);
            commandSkillComp.ParsedSpeedModifier.Add(0, 1f);
            commandSkillComp.ParsedSpeedModifier.Add(1, 1.25f);
            commandSkillComp.ParsedSpeedModifier.Add(2, 1.5f);
            commandSkillComp.ParsedSpeedModifier.Add(3, 1.75f);
            commandSkillComp.ParsedSpeedModifier.Add(4, 2f);
            commandSkillComp.ParsedSpeedModifier.Add(5, 3f);
            commandSkillComp.ParsedSpeedModifier.Add(6, 3.5f);
            commandSkillComp.EffectedDepartments = skillHolderComp.CommandSkillDepartment;
        }
    }
}

using System.Numerics;
using Robust.Shared.Prototypes;
using Content.Omu.Server.SkillSystem.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Robust.Shared.Map;
using Content.Shared.Tools.Components;
using Content.Shared.Hands;
using Content.Omu.Common.SkillSystem;
using Content.Shared.Medical.Healing;
using Content.Shared._Shitmed.Medical.Surgery.Tools;

namespace Content.Omu.Server.SkillSystem;

public sealed partial class OmuSkillSystem : CommenOmuSkillSystem
{
    public void StartUpShitmedToolCompModification(EntityUid uid, ISurgeryToolComponent comp, ref ComponentStartup args)
    {
        comp.Speed = comp.BaseSpeed;
    }
    public void ModifyShitmedToolComponent(ISurgeryToolComponent toolComp, EntityUid user, EntityUid item)
    {
        toolComp.Speed = toolComp.BaseSpeed;
        toolComp.Speed *= GetSkillSpeedModifier(user, item, out var cancle);
        toolComp.PreventUse = cancle;
    }
    public void ResetShitmedToolComponent(ISurgeryToolComponent comp)
    {
        comp.Speed = comp.BaseSpeed;
        comp.PreventUse = false;
    }
}

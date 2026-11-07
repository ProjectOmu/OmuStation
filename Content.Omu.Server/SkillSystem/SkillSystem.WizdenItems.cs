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
using Content.Omu.Common.DoAfter;

namespace Content.Omu.Server.SkillSystem;

public sealed partial class OmuSkillSystem : CommenOmuSkillSystem
{
    public void ModifyDoAfterTime(Entity<SkillRequermentComponent> ent, ref ModifyDelayOfDoAfter args)
    {
        args.Multiplier *= 1.0f / GetSkillSpeedModifier(args.User, ent.Comp, out var cancle);
        if (cancle)
        {
            args.Cancle = true;
        }
    }
}

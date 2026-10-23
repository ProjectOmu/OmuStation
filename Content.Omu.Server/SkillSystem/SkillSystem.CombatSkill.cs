using Content.Omu.Server.SkillSystem.Components;
using Content.Omu.Common.SkillSystem;
using Content.Shared.Damage.Components;
using Content.Shared.Mind.Components;
using Content.Omu.Server.SkillSystem.Events;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Omu.Server.SkillSystem;

public sealed partial class OmuSkillSystem : CommenOmuSkillSystem
{
    public void ModifyRangedWeaponComponent(Entity<SkillRequermentComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (args.User == null) return;
        //if (!TryComp<SkillRequermentComponent>(ent, out var _)) return; -> not needed as default is 1.0f
        var modifier = GetSkillSpeedModifier((EntityUid) args.User, ent.Comp, out var cancle);
        args.MaxAngle *= modifier;
        args.MinAngle *= modifier;
    }
    public void SkillStaminaModification(Entity<SkillStaminaAjustmentComponent> ent, ref SkillLevelChengeEvent args)
    {
        if (!TryComp<StaminaComponent>(ent, out var staminaComponent)) return;
        if (!TryComp<MindContainerComponent>(ent, out var mindContainer)) return;
        if (!mindContainer.HasMind) return;
        if (!TryComp<SkillHolderComponent>(mindContainer.Mind, out var skillComp)) return;
        staminaComponent.Decay = GetHighestModifier(ent.Comp.ParsedStaminaRegenModifiers, GetHighestSkillLevel(skillComp.SkillDic, ent.Comp.Skill));
        return;
    }
}

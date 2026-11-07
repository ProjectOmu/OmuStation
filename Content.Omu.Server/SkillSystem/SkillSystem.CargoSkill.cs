using Content.Omu.Common.SkillSystem;
using Content.Shared.Clothing;

namespace Content.Omu.Server.SkillSystem;

public sealed partial class OmuSkillSystem : CommenOmuSkillSystem
{
    public void ModifyCrateComponent(ClothingSpeedModifierComponent modifierComponent, EntityUid user, EntityUid item)
    {
        modifierComponent.SprintModifier = GetSkillSpeedModifier(user, item, out var _);
        modifierComponent.WalkModifier = GetSkillSpeedModifier(user, item, out var _);
    }
}

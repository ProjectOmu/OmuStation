using System.Numerics;
using Robust.Shared.Prototypes;
using Content.Omu.Server.SkillSystem.Components;
using Content.Omu.Server.SkillSystem.Events;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Robust.Shared.Map;
using Content.Shared.Tools.Components;
using Content.Shared.Hands;
using Content.Omu.Common.SkillSystem;
using Content.Omu.Shared.SkillSystem;
using Content.Shared.Medical.Healing;
using Content.Shared.Mind.Components;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Clothing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Omu.Common.DoAfter;

namespace Content.Omu.Server.SkillSystem;

public partial class OmuSkillSystem : CommenOmuSkillSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkillHolderBodyComponent, ExaminedEvent>(OnExaminSkillHolder);
        SubscribeLocalEvent<SkillRequermentComponent, ExaminedEvent>(OnExaminSkillRequierments);

        SubscribeLocalEvent<SkillGrantingComponent, UseInHandEvent>(LearnSkill);

        SubscribeLocalEvent<SkillRequermentComponent, GotEquippedHandEvent>(ModifyToolSpeed);

        SubscribeLocalEvent<SkillRequermentComponent, ComponentStartup>(RequStartUp);
        SubscribeLocalEvent<SkillRequermentComponent, ModifyDelayOfDoAfter>(ModifyDoAfterTime);
        SubscribeLocalEvent<SkillStaminaAjustmentComponent, ComponentStartup>(StaminaAjustStartup);

        SubscribeLocalEvent<RoundStartSkillGrantingComponent, MindAddedMessage>(RoundStartSkillGrantingStartup);
        SubscribeLocalEvent<RoundStartSkillGrantingComponent, ComponentStartup>(RoundStartSkillGrantingStartup);
        SubscribeLocalEvent<AntagSkillGrantingComponent, ComponentStartup>(AntagStartSkillGrantingStartup);

        //Wizden components
        SubscribeLocalEvent<SkillStaminaAjustmentComponent, SkillLevelChengeEvent>(SkillStaminaModification);
        SubscribeLocalEvent<SkillRequermentComponent, GunRefreshModifiersEvent>(ModifyRangedWeaponComponent);
    }
    public void RequStartUp(Entity<SkillRequermentComponent> ent, ref ComponentStartup args)
    {
        //parse things STARS FOR SOME REASON WE CANT MAKE DICTINARIES WITH NUMERS!!!!!
        ent.Comp.ParsedModifiers = new();
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
                ent.Comp.ParsedModifiers.Add(parsedInt, ent.Comp.SpeedModifier[iterator]);
            }
        }
    }
    public void StaminaAjustStartup(Entity<SkillStaminaAjustmentComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.ParsedStaminaRegenModifiers = new();
        foreach (var iterator in ent.Comp.StaminaRegenModifiers.Keys)
        {
            if (int.TryParse(iterator, out var parsedInt))
            {
                if (ent.Comp.StaminaRegenModifiers[iterator] == 0)
                {
                    Log.Error("WARNING SKILL COMPONENT SPEED MODIFER WAS EQUAL TO ZERO not permitted.");
                    RemComp<SkillRequermentComponent>(ent);
                    return;
                }
                ent.Comp.ParsedStaminaRegenModifiers.Add(parsedInt, ent.Comp.StaminaRegenModifiers[iterator]);
            }
        }
    }
    public void OnExaminSkillHolder(Entity<SkillHolderBodyComponent> ent, ref ExaminedEvent args)
    {
        if (!(args.Examined == args.Examiner || TryComp<TraitSkillShowoffComponent>(args.Examined, out var _)))
        {
            if (!TryComp<SkillExaminPermittedComponent>(args.Examiner, out var _)) return;
        }
        if (!TryComp<MindContainerComponent>(ent, out var mindContainer)) return;
        if (!mindContainer.HasMind) return;
        if (!TryComp<SkillHolderComponent>(mindContainer.Mind, out var skillComp)) return;
        string examinText = Loc.GetString("OMU_Skill_Examin_Text_Base");
        foreach (var iterator in skillComp.SkillDic.Keys)
        {
            examinText += '\n';
            examinText += Loc.GetString("OMU_Skill_Examin", ("Skill", iterator), ("Skill_Level", skillComp.SkillDic[iterator]));
        }
        args.PushMarkup(examinText);
    }
    public void OnExaminSkillRequierments(Entity<SkillRequermentComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.HardRequirment)
        {
            args.PushMarkup(Loc.GetString("OMU_Skill_Req_HardRequirment", ("Skill", ent.Comp.Skill), ("Skill_Level", ent.Comp.HardRequirmentMinSkill)));
        }
        args.PushMarkup(Loc.GetString("OMU_Skill_Req_Examin_Text", ("Effective_speed", GetSkillSpeedModifier(args.Examiner, ent.Comp, out var _).ToString())));
    }
    public float GetSkillSpeedModifier(EntityUid userUid, EntityUid toolUid, out bool cancle)
    {
        cancle = false;
        if (!TryComp<SkillRequermentComponent>(toolUid, out var requirmentComp)) return 1.0f;
        return GetSkillSpeedModifier(userUid, requirmentComp, out cancle);
    }
    public float GetSkillSpeedModifier(EntityUid userUid, SkillRequermentComponent requirmentsComp, out bool cancle)
    {
        cancle = false;
        if (!TryComp<MindContainerComponent>(userUid, out var mindContainer)) return 1.0f;
        if (!mindContainer.HasMind) return 1.0f;
        if (!TryComp<SkillHolderComponent>(mindContainer.Mind, out var skillComp)) return 1.0f;
        cancle = false;
        if (requirmentsComp.HardRequirment)
        {
            if (!skillComp.SkillDic.ContainsKey(requirmentsComp.HardRequirmentSkill) || skillComp.SkillDic[requirmentsComp.HardRequirmentSkill] < requirmentsComp.HardRequirmentMinSkill)
            {
                cancle = true;
                return 0f;
            }
        }
        int skillLevel = GetHighestSkillLevel(skillComp.SkillDic, requirmentsComp.Skill);
        float speedMod;
        speedMod = GetHighestModifier(requirmentsComp.ParsedModifiers, skillLevel);
        speedMod *= GetCommandSkillBonus(skillComp, (EntityUid) mindContainer.Mind);
        return speedMod;
    }
    public float GetCommandSkillBonus(SkillHolderComponent effectReciverSkillComp, EntityUid entUid)
    {
        EntityCoordinates centerCords = Transform(entUid).Coordinates;
        var refMapID = Transform(entUid).MapID;
        var commandSkillQuerry = EntityQueryEnumerator<CommandSkillComponent, SkillHolderComponent>();
        float highestSpeedMod = 1.0f;
        while (commandSkillQuerry.MoveNext(out EntityUid commandUid, out var commandSkillComp, out var skillComp))
        {
            var refCords = Transform(commandUid).Coordinates;
            if (Transform(commandUid).MapID != refMapID) continue;
            if (Vector2.Distance(refCords.Position, centerCords.Position) > commandSkillComp.Range) continue;
            float commandSpeedMod = GetHighestModifier(commandSkillComp.ParsedSpeedModifier, skillComp.SkillDic["command"]);
            if (highestSpeedMod < commandSpeedMod)
            {
                highestSpeedMod = commandSpeedMod;
            }
        }
        return highestSpeedMod;
    }
    private int GetHighestSkillLevel(Dictionary<ProtoId<SkillNamePrototype>, int> dict, HashSet<ProtoId<SkillNamePrototype>> allowed)
    {
        int returnvalue = 0;
        foreach (var iterator in dict.Keys)
        {
            if (!allowed.Contains(iterator)) continue;
            if (returnvalue < dict[iterator]) returnvalue = dict[iterator];
        }
        return returnvalue;
    }
    private float GetHighestModifier(Dictionary<int, float> dict, int skillLevel)
    {
        int psudoSL = -1;
        foreach (var iterator in dict.Keys)
        {
            if (iterator <= skillLevel)
            {
                psudoSL = iterator;
            }
        }
        if (psudoSL == -1)
        {
            return 0.1f;//fallback
        }
        return dict[psudoSL];
    }
    public void LearnSkill(Entity<SkillGrantingComponent> ent, ref UseInHandEvent args)
    {
        if (!TryComp<MindContainerComponent>(args.User, out var mindContainer)) return;
        if (!mindContainer.HasMind) return;
        if (!TryComp<SkillHolderComponent>(mindContainer.Mind, out var skillComp)) return;
        bool used = false;
        foreach (var iterator in ent.Comp.SkillDicMaxSkillLevel.Keys)
        {
            if (skillComp.SkillDic.ContainsKey(iterator))
            {
                if (skillComp.SkillDic[iterator] >= ent.Comp.SkillDicMaxSkillLevel[iterator]) continue;
                skillComp.SkillDic[iterator] = ent.Comp.SkillDicMaxSkillLevel[iterator];
                used = true;
            }
            else
            {
                skillComp.SkillDic.Add(iterator, ent.Comp.SkillDicMaxSkillLevel[iterator]);
                used = true;
            }
        }
        if (used && ent.Comp.DestroyAfterUse)
        {
            QueueDel(ent);
        }
        SkillLevelChengeEvent ev = new();
        RaiseLocalEvent(ent, ref ev);
    }
    public void ModifyToolSpeed(Entity<SkillRequermentComponent> ent, ref GotEquippedHandEvent args)
    {
        //Wizden things
        if (TryComp<ClothingSpeedModifierComponent>(args.Equipped, out var speedComp)) ModifyCrateComponent(speedComp, args.User, args.Equipped);
    }
}

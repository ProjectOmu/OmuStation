using Content.Shared.Mind;
using Content.Server.Antag;

using Content.Omu.Common.SkillSystem;
using Content.Omu.Server.SkillSystem.Components;

namespace Content.Omu.Server.SkillSystem;

//This is a temp solve for old omu, refried should get this restructured to attach this to the selected antag somehow or make it dependent on objectives
public sealed partial class OmuSkillSystem : CommenOmuSkillSystem
{
    private string _antagUniveralSkill = "Combat";
    private int _antagUniversalSkillLevel = 2;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
        {
            Log.Error($"SkillSystem.Antag {ToPrettyString(args.EntityUid):player} was selected as antag but had no mind attached, unable to add skills!");
            return;
        }
        if (!TryComp<SkillHolderComponent>(mindId, out var skillComp)) return;
        if (skillComp.SkillDic.ContainsKey(_antagUniveralSkill)) skillComp.SkillDic[_antagUniveralSkill] += _antagUniversalSkillLevel;
        else
        {
            skillComp.SkillDic.Add(_antagUniveralSkill, _antagUniversalSkillLevel);
        }
    }
}
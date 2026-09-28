using Content.Shared.Humanoid;
using Content.Shared.Revolutionary.Components;
using Content.Server.Mind;
using Robust.Shared.Timing;
using Content.Shared._Omu.Revs;
using Content.Shared._EinsteinEngines.Language.Systems;
using Content.Shared._EinsteinEngines.Language.Components;

namespace Content.Server._Omu.Revs;

public sealed class MoraleHarmerAreaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedLanguageSystem _language = default!;
    public override void Initialize()
    {
        base.Initialize();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var query = EntityManager.EntityQuery<MoraleHarmerAreaComponent>();

        foreach (var moraleHarmer in query)
        {
            if (TerminatingOrDeleted(moraleHarmer.Owner))
                continue;

            moraleHarmer.UpdateAccumulator += frameTime;

            if (moraleHarmer.UpdateAccumulator >= moraleHarmer.UpdateTimer)
            {
                moraleHarmer.UpdateAccumulator -= moraleHarmer.UpdateTimer;
                AreaChange(new Entity<MoraleHarmerAreaComponent>(moraleHarmer.Owner, moraleHarmer));
            }
        }
    }

    public void AreaChange(Entity<MoraleHarmerAreaComponent> ent)
    {
        var xform = Transform(ent);
        var lookup = _lookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range);
        foreach (var target in lookup)
        {
            if (!_mind.TryGetMind(target, out _, out _) || !HasComp<HumanoidAppearanceComponent>(target) || HasComp<RevolutionaryComponent>(target))
                continue;

            if (HasComp<MoraleComponent>(target) && !HasComp<MoralePassedComponent>(target))
            {
                var ev = new MoraleChangedArgs
                {
                    Amount = ent.Comp.MoraleChange,

                    User = ent,
                };
                RaiseLocalEvent(target, ev);
            }
            else if (!HasComp<MoralePassedComponent>(target))
                EnsureComp<MoraleComponent>(target);       //Ensure morale comp.
        }
    }

    public void AreaChange(EntityUid ent, float amount, float range, string? lang)
    {
        var xform = Transform(ent);
        var lookup = _lookup.GetEntitiesInRange(xform.Coordinates, range);
        foreach (var target in lookup)
        {
            if (!_mind.TryGetMind(target, out _, out _) || !HasComp<HumanoidAppearanceComponent>(target) || HasComp<RevolutionaryComponent>(target))
                continue;

            if (lang is not null)
            {
                if (EntityManager.TryGetComponent<LanguageSpeakerComponent>(ent, out var speakerComponent))      //If they dont have the speaker comp it doesnt really matter - they probably aren't humanoid and thus it failed earlier
                    if (!_language.CanUnderstand(target, speakerComponent.CurrentLanguage))
                        return; //the target does not understand the speaker's language, so the conversion fails
            }

            if (TryComp<MoraleComponent>(target, out var morale) && !HasComp<MoralePassedComponent>(target))
            {
                var ev = new MoraleChangedArgs
                {
                    Amount = amount,

                    User = ent,
                };
                RaiseLocalEvent(target, ev);
            }
            else if (!HasComp<MoralePassedComponent>(target))
                EnsureComp<MoraleComponent>(target);       //Ensure morale comp.
        }
    }
}

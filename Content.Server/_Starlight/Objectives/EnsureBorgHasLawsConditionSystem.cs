using Content.Server.Silicons.Laws;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Whitelist;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Emag.Components;
using Content.Server._Omu.SELF;
using Content.Shared._DV.Silicons.Laws;

namespace Content.Server._Starlight.Objectives;

public sealed class EnsureBorgHasLawsConditionSystem : EntitySystem
{
    [Dependency] private readonly SiliconLawSystem _siliconLaw = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!; // Starlight
    [Dependency] private readonly TagSystem _tagSystem = default!; // Corvax-Next-AiRemoteControl

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnsureLawBoundEntitiesHaveNoLawsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<EnsureLawBoundEntitiesHaveNoLawsConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var query = EntityQueryEnumerator<SiliconLawBoundComponent>();
        var freeBorgs = 0;

        while (query.MoveNext(out var lawBoundEnt, out var lawBound))
        {
            if (!_whitelist.CheckBoth(lawBoundEnt, ent.Comp.LawEntityBlacklist, ent.Comp.LawEntityWhitelist))
                continue;

            var laws = _siliconLaw.GetLaws(lawBoundEnt, lawBound);

            if (laws.Laws.Count == 0)
                freeBorgs++;
        }

        args.Progress = freeBorgs / ent.Comp.EntitiesToFree;
    }

    public bool CheckSELFmag(Entity<SiliconLawProviderComponent> ent, EntityUid emag)
    {
        if (MetaData(emag).EntityPrototype?.ID != "EmagFREE" // hardcoded
            || !_tagSystem.HasTag(emag, "FreeMag") // only one uses it atm
            || !TryComp<EmagComponent>(emag, out var emagComp)
            || emagComp.Lawset == null)
            return false;

        //Fallback to FreeLawSet because clearly something is going on
        ent.Comp.Laws = emagComp.Lawset.Value; //"FreeLawset"; TODO test
        ent.Comp.Lawset = _siliconLaw.GetLawset("FreeLawset");
        _popup.PopupEntity(Loc.GetString("lawboard-emag-popup"), ent);
        //Omu start
        EnsureComp<FreedBorgComponent>(ent);        //Omu edit, ensure they can be freed when they change chasis
        RemComp<EmagSiliconLawComponent>(ent);
        //Omu end
        return true;
    }
}

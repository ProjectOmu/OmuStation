using Content.Server.Silicons.Laws;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Whitelist;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Emag.Components;
using Content.Shared.Wires;

namespace Content.Server._Starlight.Objectives;

public sealed class EnsureBorgHasLawsConditionSystem : EntitySystem
{
    [Dependency] private readonly SiliconLawSystem _siliconLaw = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly IEntityManager _entMan = default!; // Starlight
    [Dependency] private readonly SharedPopupSystem _popup = default!; // Starlight
    [Dependency] private readonly TagSystem _tagSystem = default!; // Corvax-Next-AiRemoteControl

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnsureLawBoundEntitiesHaveNoLawsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<SiliconLawProviderComponent, GotEmaggedEvent>(OnGotEmagged);
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

        args.Progress = freeBorgs / (float)ent.Comp.EntitiesToFree;
    }

    private void OnGotEmagged(Entity<SiliconLawProviderComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (args.EmagUid == null)
            return;

        if (!TryComp<EmagSiliconLawComponent>(ent, out var emagComponent))
            return;

        if (emagComponent.RequireOpenPanel &&
            TryComp<WiresPanelComponent>(ent, out var panel) &&
            !panel.Open)
        {
            _popup.PopupClient(Loc.GetString("law-emag-require-panel"), ent, args.UserUid);
            return;
        }

        if (_tagSystem.HasTag(args.EmagUid.Value, "FreeMag"))
        {
            if (TryComp<EmagComponent>(args.EmagUid.Value, out var emag) && emag.Lawset != null){
                var lawset = emag.Lawset.Value; //Fallback to FreeLawSet because clearly something is going on
                ent.Comp.Laws = lawset; //"FreeLawset"; TODO test
                ent.Comp.Lawset = _siliconLaw.GetLawset("FreeLawset");
                _popup.PopupEntity(Loc.GetString("lawboard-emag-popup"), ent);
            }
        }

        args.Repeatable = true;
        args.Handled = true;
    }
}

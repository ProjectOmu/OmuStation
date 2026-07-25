using Content.Shared.Examine;
using Robust.Shared.Utility;
using Content.Server.GameTicking;
using Content.Goobstation.Shared.CustomFactionIcons;
using Content.Server._Goobstation.Chaplain;
using Content.Server._Goobstation.Chaplain.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Components;

namespace Content.Server._Omu.Entities.Heretic;

public sealed class FascinationSystem: EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FascinationComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FascinationComponent, FascinationChangedArgs>(OnChange);
        SubscribeLocalEvent<FascinationComponent, ComponentStartup>(OnStartup);
    }
    private void OnStartup(EntityUid uid, FascinationComponent component, ComponentStartup args)
    {
        if (HasComp<SeeHereticFixturesComponent>(uid))
            component.Naturalsight = true;
    }

    private void OnExamined(Entity<FascinationComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        var Value = comp.FascinationValue;
        string message;

        if (Value == 1)
        {
            message = Loc.GetString("fascination-examine-1");
        }
        else if (Value == 2)
        {
            message = Loc.GetString("fascination-examine-2");
        }
        else if (Value == 3)
        {
            message = Loc.GetString("fascination-examine-3");
        }
        else if (Value == 4)
        {
            message = Loc.GetString("fascination-examine-4");
        }
        else if (Value == 5)
        {
            message = Loc.GetString("fascination-examine-5");
        }
        else
        {
            message = Loc.GetString("fascination-examine-5");
        }
        args.PushMarkup(message ?? Loc.GetString("fascination-examine-1"));
    }
    private void OnChange(Entity<FascinationComponent> ent, ref FascinationChangedArgs args)
    {
        ent.Comp.FascinationValue = args.Amount + ent.Comp.FascinationValue; //increment the fascination value by the amount of knowledge gained!

        float fascvalue = ent.Comp.FascinationValue;

        if (fascvalue < 5)
        {
            if (ent.Comp.Naturalsight == false && ent.Comp.AlteredVision == true)
            {
                RemComp<SeeHereticFixturesComponent>(ent);
                ent.Comp.AlteredVision = false;
                _eye.RefreshVisibilityMask(ent.Owner);
            }
            if (ent.Comp.AlteredFaction == true)
            {
                var userFactionIcons = EnsureComp<CustomFactionIconsComponent>(ent);    //Make them un-valid to the mirror maiden
                userFactionIcons.FactionIcons.Remove(ent.Comp.IconToAdd);
                _faction.RemoveFaction(ent.Owner, ent.Comp.FactionToAdd); // remove the faction
                ent.Comp.AlteredFaction = false;
                Dirty(ent.Owner, userFactionIcons);
            }
        }
        if (fascvalue <= 0)
        {
            RemComp<FascinationComponent>(ent);
        }
        if (fascvalue >= 5)
        {
            if (ent.Comp.Naturalsight == false)
            {
                _adminLog.Add(LogType.AdminMessage, LogImpact.Extreme,
                $"{ent} has fascination 5, making valid");
                EnsureComp<SeeHereticFixturesComponent>(ent);
                ent.Comp.AlteredVision = true;
                _eye.RefreshVisibilityMask(ent.Owner);
            }
            if (ent.Comp.AlteredFaction != true)
            {
                ent.Comp.AlteredFaction = true;
                var userFactionIcons = EnsureComp<CustomFactionIconsComponent>(ent);    //Make them valid to the mirror maiden
                userFactionIcons.FactionIcons.Add(ent.Comp.IconToAdd);
                _faction.AddFaction(ent.Owner, ent.Comp.FactionToAdd); //Give them the faction so AI works
                Dirty(ent.Owner, userFactionIcons);
            }
            _gameTicker.StartGameRule("BlueMaidenSpawn", out _);

        }
    }
}

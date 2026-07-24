using Content.Shared.Examine;
using Robust.Shared.Utility;
using Content.Server.GameTicking;
using Content.Goobstation.Shared.CustomFactionIcons;
using Content.Server._Goobstation.Chaplain;
using Content.Server._Goobstation.Chaplain.Components;

namespace Content.Server._Omu.Entities.Heretic;

public sealed class FascinationSystem: EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
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
        ent.Comp.FascinationValue += args.Amount; //increment the fascination value by the amount of knowledge gained!

        float fascvalue = ent.Comp.FascinationValue;

        if (fascvalue < 5)
        {
            if (ent.Comp.Naturalsight == false & ent.Comp.AlteredVision == true)
                RemComp<SeeHereticFixturesComponent>(ent);
            if (ent.Comp.AlteredFaction == true)
            {
                var userFactionIcons = EnsureComp<CustomFactionIconsComponent>(ent);    //Make them un-valid to the mirror maiden
                userFactionIcons.FactionIcons.Remove(ent.Comp.IconToAdd);
            }
        }
        if (fascvalue <= 0);
        {
            RemComp<FascinationComponent>(ent);
        }
        if (fascvalue >= 5)
        {
            if (ent.Comp.Naturalsight == false)
            {
                EnsureComp<SeeHereticFixturesComponent>(ent);
                ent.Comp.AlteredVision = true;
                _gameTicker.StartGameRule("BlueMaidenSpawn", out _);
                ent.Comp.AlteredFaction = true;
                var userFactionIcons = EnsureComp<CustomFactionIconsComponent>(ent);    //Make them valid to the mirror maiden
                userFactionIcons.FactionIcons.Add(ent.Comp.IconToAdd);
            }
        }
    }
}

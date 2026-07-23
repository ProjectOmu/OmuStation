using Content.Shared.Examine;
using NetCord;
using Robust.Shared.Utility;
using YamlDotNet.Core.Tokens;

namespace Content.Server._Omu.Entities.Heretic;

public sealed class FascinationSystem: EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FascinationComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FascinationComponent, FascinationChangedArgs>(OnChange);
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
    }
}

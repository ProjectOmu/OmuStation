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
    }

    private void OnExamined(Entity<FascinationComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        var Value = comp.FascinationValue;
        if (Value == 1)
        {}
        Loc.GetString("fascination-examine-1");
        Loc.GetString("fascination-examine-2");
        Loc.GetString("fascination-examine-3");
        Loc.GetString("fascination-examine-4");
        Loc.GetString("fascination-examine-5");
        args.PushMarkup(comp.ExamineMessage ?? Loc.GetString("Fascination-0"));
        Dirty(ent);
    }
}
